using System.Threading.Channels;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh;

public sealed partial class SshClient
{
    private const string UserAuthService = "ssh-userauth";
    private const string ConnectionService = "ssh-connection";
    private const int MaxPromptRounds = 3;
    private const int MaxKeyboardInteractivePrompts = 16;

    private readonly Channel<byte[]> _authenticationMessages = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private volatile bool _isAuthenticated;
    private IReadOnlyList<string>? _serverSignatureAlgorithms;

    public bool IsAuthenticated => _isAuthenticated;

    // A login that needed no proof is refused when the options say so.
    public async Task AuthenticateAsync(IReadOnlyList<ISshSigner> keys, IAuthenticationPrompts prompts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(prompts);
        await RequestUserAuthServiceAsync(cancellationToken).ConfigureAwait(false);
        var login = new LoginState(keys, prompts);
        var reply = await TryNoneAsync(login, cancellationToken).ConfigureAwait(false);
        while (reply.Failure is { } failure)
        {
            reply = await TryNextMethodAsync(login, failure, cancellationToken).ConfigureAwait(false);
        }

        if (login.ProvedIdentity || !_options.RefuseTrivialAuthentication)
        {
            return;
        }

        await CloseAsync(DisconnectReason.ByApplication, "Login without credentials refused.").ConfigureAwait(false);
        throw new SshAuthenticationException("The server let us in without checking any credentials, which these connection settings refuse.");
    }

    private void HandleAuthenticationMessage(byte[] payload)
    {
        if (_isAuthenticated)
        {
            throw new SshProtocolException($"Message {payload[0]} arrived after login.", DisconnectReason.ProtocolError);
        }

        if (payload[0] == MessageNumber.ExtInfo)
        {
            ReadExtensionInfo(payload);
            return;
        }

        if (payload[0] == MessageNumber.UserAuthSuccess)
        {
            // Set here, in the read loop, so connection messages that follow at once are accepted.
            _isAuthenticated = true;
        }

        _authenticationMessages.Writer.TryWrite(payload);
    }

    private void ReadExtensionInfo(byte[] payload)
    {
        var reader = new SshReader(payload);
        reader.ReadByte();
        var count = reader.ReadUInt32();
        if (count > 64)
        {
            throw new SshProtocolException("The server sent too many extensions.", DisconnectReason.ProtocolError);
        }

        for (var index = 0; index < count; index++)
        {
            var name = reader.ReadAsciiString();
            if (string.Equals(name, "server-sig-algs", StringComparison.Ordinal))
            {
                _serverSignatureAlgorithms = reader.ReadNameList();
            }
            else
            {
                reader.ReadString();
            }
        }
    }

    private async Task<byte[]> ReceiveAuthenticationMessageAsync(IAuthenticationPrompts? prompts, CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[] payload;
            try
            {
                payload = await _authenticationMessages.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException exception)
            {
                throw new SshDisconnectedException(DisconnectMessage ?? "The connection closed during login.", exception.InnerException ?? exception);
            }

            if (payload[0] != MessageNumber.UserAuthBanner)
            {
                return payload;
            }

            ShowBanner(payload, prompts);
        }
    }

    private static void ShowBanner(byte[] payload, IAuthenticationPrompts? prompts)
    {
        var reader = new SshReader(payload);
        reader.ReadByte();
        var text = ServerText.Sanitize(reader.ReadUtf8String());
        prompts?.ShowBanner(text);
    }

    private async Task RequestUserAuthServiceAsync(CancellationToken cancellationToken)
    {
        await SendAsync(new SshWriter().WriteByte(MessageNumber.ServiceRequest).WriteString(UserAuthService).ToArray(), cancellationToken).ConfigureAwait(false);
        var reply = await ReceiveAuthenticationMessageAsync(prompts: null, cancellationToken).ConfigureAwait(false);
        var reader = new SshReader(reply);
        var isAccepted = reader.ReadByte() == MessageNumber.ServiceAccept && string.Equals(reader.ReadAsciiString(), UserAuthService, StringComparison.Ordinal);
        if (isAccepted)
        {
            return;
        }

        throw new SshProtocolException("The server didn't accept the login service request.", DisconnectReason.ServiceNotAvailable);
    }

    private SshWriter BeginRequest(string method) =>
        new SshWriter().WriteByte(MessageNumber.UserAuthRequest).WriteString(_options.UserName).WriteString(ConnectionService).WriteString(method);

    private async Task<AuthenticationReply> TryNoneAsync(LoginState login, CancellationToken cancellationToken)
    {
        await SendAsync(BeginRequest("none").ToArray(), cancellationToken).ConfigureAwait(false);
        return login.Remember(ReadReply(await ReceiveAuthenticationMessageAsync(login.Prompts, cancellationToken).ConfigureAwait(false)));
    }

    private async Task<AuthenticationReply> TryNextMethodAsync(LoginState login, AuthenticationFailure failure, CancellationToken cancellationToken)
    {
        if (failure.Allows("publickey") && login.NextKey() is { } key)
        {
            return await TryPublicKeyAsync(login, key, cancellationToken).ConfigureAwait(false);
        }

        if (failure.Allows("keyboard-interactive") && login.KeyboardInteractiveRounds < MaxPromptRounds)
        {
            login.KeyboardInteractiveRounds++;
            return await TryKeyboardInteractiveAsync(login, cancellationToken).ConfigureAwait(false);
        }

        if (failure.Allows("password") && login.PasswordRounds < MaxPromptRounds)
        {
            login.PasswordRounds++;
            return await TryPasswordAsync(login, cancellationToken).ConfigureAwait(false);
        }

        throw new SshAuthenticationException($"Login to {_options.UserName}@{_options.Host} failed: no more ways to log in. The server allows: {string.Join(", ", failure.Methods)}.");
    }

    private async Task<AuthenticationReply> TryPublicKeyAsync(LoginState login, ISshSigner key, CancellationToken cancellationToken)
    {
        var algorithm = ChooseSignatureAlgorithm(key.KeyType);
        if (algorithm is null)
        {
            return AuthenticationReply.Failed(login.LastFailure);
        }

        var query = BeginRequest("publickey").WriteBoolean(false).WriteString(algorithm).WriteString(key.PublicKeyBlob.Span).ToArray();
        await SendAsync(query, cancellationToken).ConfigureAwait(false);
        var answer = await ReceiveAuthenticationMessageAsync(login.Prompts, cancellationToken).ConfigureAwait(false);
        if (answer[0] != MessageNumber.UserAuthPublicKeyOk)
        {
            return login.Remember(ReadReply(answer));
        }

        var signed = new SshWriter()
            .WriteString(_keyExchange.SessionId)
            .WriteByte(MessageNumber.UserAuthRequest)
            .WriteString(_options.UserName)
            .WriteString(ConnectionService)
            .WriteString("publickey")
            .WriteBoolean(true)
            .WriteString(algorithm)
            .WriteString(key.PublicKeyBlob.Span)
            .ToArray();
        var signature = await key.SignAsync(signed, algorithm, cancellationToken).ConfigureAwait(false);
        var request = BeginRequest("publickey").WriteBoolean(true).WriteString(algorithm).WriteString(key.PublicKeyBlob.Span).WriteString(signature).ToArray();
        login.ProvedIdentity = true;
        await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return login.Remember(ReadReply(await ReceiveAuthenticationMessageAsync(login.Prompts, cancellationToken).ConfigureAwait(false)));
    }

    // RSA uses SHA-2 only, as the server advertises.
    private string? ChooseSignatureAlgorithm(string keyType)
    {
        if (!string.Equals(keyType, AlgorithmNames.RsaSha1, StringComparison.Ordinal))
        {
            return keyType;
        }

        var advertised = _serverSignatureAlgorithms;
        if (advertised is null || advertised.Contains(AlgorithmNames.RsaSha512, StringComparer.Ordinal))
        {
            return AlgorithmNames.RsaSha512;
        }

        return advertised.Contains(AlgorithmNames.RsaSha256, StringComparer.Ordinal) ? AlgorithmNames.RsaSha256 : null;
    }

    private async Task<AuthenticationReply> TryKeyboardInteractiveAsync(LoginState login, CancellationToken cancellationToken)
    {
        await SendAsync(BeginRequest("keyboard-interactive").WriteString(string.Empty).WriteString(string.Empty).ToArray(), cancellationToken).ConfigureAwait(false);
        while (true)
        {
            var message = await ReceiveAuthenticationMessageAsync(login.Prompts, cancellationToken).ConfigureAwait(false);
            if (message[0] != MessageNumber.UserAuthInfoRequest)
            {
                return login.Remember(ReadReply(message));
            }

            var request = ReadInfoRequest(message);
            var answers = await login.Prompts.AnswerAsync(request, cancellationToken).ConfigureAwait(false);
            if (answers is null || answers.Count != request.Prompts.Count)
            {
                login.KeyboardInteractiveRounds = MaxPromptRounds;
                return await TryNoneAsync(login, cancellationToken).ConfigureAwait(false);
            }

            login.ProvedIdentity |= answers.Count > 0;
            var response = new SshWriter().WriteByte(MessageNumber.UserAuthInfoResponse).WriteUInt32((uint)answers.Count);
            foreach (var answer in answers)
            {
                response.WriteString(answer);
            }

            await SendAsync(response.ToArray(), cancellationToken).ConfigureAwait(false);
        }
    }

    private static KeyboardInteractiveRequest ReadInfoRequest(byte[] message)
    {
        var reader = new SshReader(message);
        reader.ReadByte();
        var name = ServerText.Sanitize(reader.ReadUtf8String(), 256);
        var instruction = ServerText.Sanitize(reader.ReadUtf8String(), 1024);
        reader.ReadString();
        var count = reader.ReadUInt32();
        if (count > MaxKeyboardInteractivePrompts)
        {
            throw new SshProtocolException("The server asked too many questions at once.", DisconnectReason.ProtocolError);
        }

        var prompts = new List<KeyboardInteractivePrompt>((int)count);
        for (var index = 0; index < count; index++)
        {
            prompts.Add(new KeyboardInteractivePrompt(ServerText.Sanitize(reader.ReadUtf8String(), 256), reader.ReadBoolean()));
        }

        return new KeyboardInteractiveRequest(name, instruction, prompts);
    }

    private async Task<AuthenticationReply> TryPasswordAsync(LoginState login, CancellationToken cancellationToken)
    {
        var password = await login.Prompts.ReadPasswordAsync(_options.UserName, _options.Host, cancellationToken).ConfigureAwait(false);
        if (password is null)
        {
            login.PasswordRounds = MaxPromptRounds;
            return AuthenticationReply.Failed(login.LastFailure);
        }

        login.ProvedIdentity = true;
        await SendAsync(BeginRequest("password").WriteBoolean(false).WriteString(password).ToArray(), cancellationToken).ConfigureAwait(false);
        var reply = await ReceiveAuthenticationMessageAsync(login.Prompts, cancellationToken).ConfigureAwait(false);
        if (reply[0] == MessageNumber.UserAuthPasswordChangeRequest)
        {
            throw new SshAuthenticationException("The server requires a new password. Change it in a terminal, then connect again.");
        }

        return login.Remember(ReadReply(reply));
    }

    private static AuthenticationReply ReadReply(byte[] payload)
    {
        var reader = new SshReader(payload);
        switch (reader.ReadByte())
        {
            case MessageNumber.UserAuthSuccess:
                return AuthenticationReply.Succeeded;
            case MessageNumber.UserAuthFailure:
                var methods = reader.ReadNameList();
                reader.ReadBoolean();
                return AuthenticationReply.Failed(new AuthenticationFailure(methods));
            default:
                throw new SshProtocolException($"Unexpected login message {payload[0]}.", DisconnectReason.ProtocolError);
        }
    }

    private sealed record AuthenticationFailure(IReadOnlyList<string> Methods)
    {
        public bool Allows(string method) => Methods.Contains(method, StringComparer.Ordinal);
    }

    private sealed record AuthenticationReply(AuthenticationFailure? Failure)
    {
        public static readonly AuthenticationReply Succeeded = new(Failure: null);

        public static AuthenticationReply Failed(AuthenticationFailure failure) => new(failure);
    }

    private sealed class LoginState(IReadOnlyList<ISshSigner> keys, IAuthenticationPrompts prompts)
    {
        private int _nextKey;

        public IAuthenticationPrompts Prompts { get; } = prompts;

        public AuthenticationFailure LastFailure { get; private set; } = new([]);

        public int KeyboardInteractiveRounds { get; set; }

        public int PasswordRounds { get; set; }

        public bool ProvedIdentity { get; set; }

        public ISshSigner? NextKey() => _nextKey < keys.Count ? keys[_nextKey++] : null;

        public AuthenticationReply Remember(AuthenticationReply reply)
        {
            LastFailure = reply.Failure ?? LastFailure;
            return reply;
        }
    }
}
