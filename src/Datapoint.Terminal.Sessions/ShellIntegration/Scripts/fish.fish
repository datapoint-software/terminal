# Datapoint Terminal shell integration for fish, loaded with --init-command after the user's config.
# Emits OSC 133 (prompt, input, output, exit code), OSC 633;E (the command line, with the session's nonce) and OSC 7 (working folder).

if set -q __dpt_loaded
    exit
end
set -g __dpt_loaded 1
# Over SSH the scripts arrive in a temporary folder; fish has read this whole file already, so the folder can go.
if set -q DATAPOINT_CLEANUP
    rm -rf -- $DATAPOINT_CLEANUP
    set -e DATAPOINT_CLEANUP
end
set -g __dpt_nonce $DATAPOINT_NONCE
set -e DATAPOINT_NONCE
set -g __dpt_in_command 0

function __dpt_escape
    string split \n -- $argv[1] | string replace -a -- '\\' '\\\\' | string replace -a -- ';' '\\x3b' | string join '\\x0a'
end

function __dpt_preexec --on-event fish_preexec
    set -g __dpt_in_command 1
    printf '\e]633;E;%s;%s\a\e]133;C\a' (__dpt_escape $argv[1]) $__dpt_nonce
end

function __dpt_postexec --on-event fish_postexec
    set -l ret $status
    if test $__dpt_in_command = 1
        printf '\e]133;D;%s\a' $ret
    end
    set -g __dpt_in_command 0
end

functions -q fish_prompt; and functions -c fish_prompt __dpt_original_prompt
function fish_prompt
    printf '\e]7;file://%s%s\a' (prompt_hostname) (string replace -a ' ' '%20' -- $PWD)
    printf '\e]133;A\a'
    functions -q __dpt_original_prompt; and __dpt_original_prompt
    printf '\e]133;B\a'
end
