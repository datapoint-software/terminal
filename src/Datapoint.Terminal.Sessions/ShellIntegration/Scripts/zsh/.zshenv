# Datapoint Terminal: zsh reads this from our folder (ZDOTDIR); load the user's own .zshenv.
__dpt_zdotdir=$ZDOTDIR
ZDOTDIR=${DATAPOINT_USER_ZDOTDIR:-$HOME}
[[ -r $ZDOTDIR/.zshenv ]] && . $ZDOTDIR/.zshenv
DATAPOINT_USER_ZDOTDIR=$ZDOTDIR
ZDOTDIR=$__dpt_zdotdir
