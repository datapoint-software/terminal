# Datapoint Terminal: zsh reads this from our folder (ZDOTDIR); load the user's own .zshrc, then our integration.
__dpt_zdotdir=$ZDOTDIR
ZDOTDIR=${DATAPOINT_USER_ZDOTDIR:-$HOME}
[[ -r $ZDOTDIR/.zshrc ]] && . $ZDOTDIR/.zshrc
DATAPOINT_USER_ZDOTDIR=$ZDOTDIR
. $__dpt_zdotdir/integration.zsh
if [[ -o login ]]; then
    ZDOTDIR=$__dpt_zdotdir
else
    ZDOTDIR=$DATAPOINT_USER_ZDOTDIR
    unset DATAPOINT_USER_ZDOTDIR __dpt_zdotdir
fi
