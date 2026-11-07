# Datapoint Terminal: zsh reads this from our folder (ZDOTDIR); load the user's own .zlogin.
__dpt_zdotdir=$ZDOTDIR
ZDOTDIR=${DATAPOINT_USER_ZDOTDIR:-$HOME}
[[ -r $ZDOTDIR/.zlogin ]] && . $ZDOTDIR/.zlogin
DATAPOINT_USER_ZDOTDIR=$ZDOTDIR
ZDOTDIR=$__dpt_zdotdir
# The last file a login shell reads: give the user their own ZDOTDIR back.
ZDOTDIR=$DATAPOINT_USER_ZDOTDIR
unset DATAPOINT_USER_ZDOTDIR __dpt_zdotdir
# Over SSH the scripts arrive in a temporary folder; this is the last of them a login shell reads.
if [[ -n ${DATAPOINT_CLEANUP:-} ]]; then rm -rf -- $DATAPOINT_CLEANUP; unset DATAPOINT_CLEANUP; fi
