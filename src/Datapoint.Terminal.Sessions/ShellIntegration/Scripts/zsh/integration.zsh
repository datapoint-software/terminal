# Datapoint Terminal shell integration for zsh, loaded from our .zshrc after the user's own.
# Emits OSC 133 (prompt, input, output, exit code), OSC 633;E (the command line, with the session's nonce) and OSC 7 (working folder).

[[ -n ${__dpt_loaded:-} ]] && return
typeset -g __dpt_loaded=1
typeset -g __dpt_nonce=${DATAPOINT_NONCE:-}
unset DATAPOINT_NONCE
typeset -g __dpt_in_command=0

__dpt_escape() {
    local text=$1
    text=${text//\\/\\\\}
    text=${text//;/\\x3b}
    text=${text//$'\n'/\\x0a}
    text=${text//$'\r'/\\x0d}
    text=${text//$'\e'/\\x1b}
    text=${text//$'\a'/\\x07}
    builtin printf '%s' "$text"
}

__dpt_precmd() {
    local ret=$?
    if (( __dpt_in_command )); then
        builtin printf '\e]133;D;%s\a' "$ret"
    fi
    __dpt_in_command=0
    local dir=${PWD//\%/%25}
    builtin printf '\e]7;file://%s%s\a' "${HOST:-}" "${dir// /%20}"
    return $ret
}

__dpt_wrap_prompt() {
    [[ $PS1 == *'133;A'* ]] || PS1=$'%{\e]133;A\a%}'$PS1$'%{\e]133;B\a%}'
}

__dpt_preexec() {
    __dpt_in_command=1
    builtin printf '\e]633;E;%s;%s\a\e]133;C\a' "$(__dpt_escape "$1")" "$__dpt_nonce"
}

typeset -ga precmd_functions preexec_functions
precmd_functions=(__dpt_precmd $precmd_functions __dpt_wrap_prompt)
preexec_functions+=(__dpt_preexec)
