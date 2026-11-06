# Datapoint Terminal shell integration for PowerShell (Windows PowerShell 5.1 and PowerShell 7), loaded with -NoExit -Command after the user's profile.
# Emits OSC 133 (prompt, input, output, exit code), OSC 633;E (the command line, with the session's nonce) and OSC 7 (working folder).

if ($Global:__DptLoaded) { return }
$Global:__DptLoaded = $true
$Global:__DptNonce = $env:DATAPOINT_NONCE
Remove-Item Env:DATAPOINT_NONCE -ErrorAction SilentlyContinue
$Global:__DptInCommand = $false
$Global:__DptEsc = [char]27
$Global:__DptBel = [char]7
$Global:__DptOriginalPrompt = $function:prompt

function Global:__DptEscape([string]$Text) {
    $Text.Replace('\', '\\').Replace(';', '\x3b').Replace("`n", '\x0a').Replace("`r", '\x0d').Replace([string][char]27, '\x1b').Replace([string][char]7, '\x07')
}

function Global:prompt {
    $succeeded = $?
    $code = $Global:LASTEXITCODE
    $e = $Global:__DptEsc
    $b = $Global:__DptBel
    $out = ''
    if ($Global:__DptInCommand) {
        $status = if ($succeeded) { 0 } elseif ($code) { $code } else { 1 }
        $out += "$e]133;D;$status$b"
    }
    $Global:__DptInCommand = $false
    if ($PWD.Provider.Name -eq 'FileSystem') {
        $path = $PWD.ProviderPath.Replace('\', '/').Replace('%', '%25').Replace(' ', '%20')
        if (-not $path.StartsWith('/')) { $path = '/' + $path }
        $out += "$e]7;file://$([System.Net.Dns]::GetHostName())$path$b"
    }
    $out += "$e]133;A$b" + (& $Global:__DptOriginalPrompt) + "$e]133;B$b"
    $Global:LASTEXITCODE = $code
    $out
}

if (Test-Path Function:\PSConsoleHostReadLine) {
    $Global:__DptOriginalReadLine = $function:PSConsoleHostReadLine
    function Global:PSConsoleHostReadLine {
        $line = & $Global:__DptOriginalReadLine
        $Global:__DptInCommand = $true
        [Console]::Write("$($Global:__DptEsc)]633;E;$(__DptEscape $line);$($Global:__DptNonce)$($Global:__DptBel)$($Global:__DptEsc)]133;C$($Global:__DptBel)")
        $line
    }
}
