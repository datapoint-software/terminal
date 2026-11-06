# Datapoint Terminal shell integration for bash (3.2 and later).
# Loaded with --init-file instead of ~/.bashrc, so it loads the user's own files first.
# Emits OSC 133 (prompt, input, output, exit code), OSC 633;E (the command line, with the session's nonce) and OSC 7 (working folder).

if [ -n "${__dpt_loaded:-}" ]; then return; fi
__dpt_loaded=1
__dpt_nonce="${DATAPOINT_NONCE:-}"
unset DATAPOINT_NONCE

if [ "${DATAPOINT_LOGIN:-}" = 1 ]; then
    unset DATAPOINT_LOGIN
    [ -r /etc/profile ] && . /etc/profile
    if [ -r ~/.bash_profile ]; then . ~/.bash_profile
    elif [ -r ~/.bash_login ]; then . ~/.bash_login
    elif [ -r ~/.profile ]; then . ~/.profile
    fi
else
    [ -r ~/.bashrc ] && . ~/.bashrc
fi

__dpt_in_command=0
__dpt_in_prompt=0

__dpt_escape() {
    local text="$1" out="" char i
    for (( i = 0; i < ${#text}; i++ )); do
        char="${text:i:1}"
        case "$char" in
            '\') out+='\\' ;;
            ';') out+='\x3b' ;;
            $'\n') out+='\x0a' ;;
            $'\r') out+='\x0d' ;;
            $'\e') out+='\x1b' ;;
            $'\a') out+='\x07' ;;
            *) out+="$char" ;;
        esac
    done
    builtin printf '%s' "$out"
}

__dpt_precmd() {
    local status=$?
    __dpt_in_prompt=1
    if [ "$__dpt_in_command" = 1 ]; then
        builtin printf '\e]133;D;%s\a' "$status"
    fi
    __dpt_in_command=0
    local path="${PWD//%/%25}"
    builtin printf '\e]7;file://%s%s\a' "${HOSTNAME:-}" "${path// /%20}"
    return $status
}

__dpt_wrap_prompt() {
    case "$PS1" in
        *'133;A'*) ;;
        *) PS1='\[\e]133;A\a\]'"$PS1"'\[\e]133;B\a\]' ;;
    esac
    __dpt_in_prompt=0
}

__dpt_preexec() {
    [ "$__dpt_in_prompt" = 1 ] && return
    case "$BASH_COMMAND" in __dpt_precmd*) return ;; esac
    [ "$__dpt_in_command" = 1 ] && return
    [ -n "${COMP_LINE:-}" ] && return
    __dpt_in_command=1
    local line HISTTIMEFORMAT=
    line="$(builtin history 1)"
    line="${line#*[0-9]  }"
    builtin printf '\e]633;E;%s;%s\a\e]133;C\a' "$(__dpt_escape "$line")" "$__dpt_nonce"
}

PROMPT_COMMAND="__dpt_precmd${PROMPT_COMMAND:+; $PROMPT_COMMAND}; __dpt_wrap_prompt"
trap '__dpt_preexec' DEBUG
