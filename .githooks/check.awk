# Checks text about to be committed for secrets and real infrastructure details (see CLAUDE.md, Working with Git).
# Reads a staged diff (mode "diff") or a commit message (mode "message"); prints each finding and exits 1 if there's any.
#   allowed:   a file of exact values that are fine to commit (e.g. public addresses tests use), one per line.
#   forbidden: a file of words that must never be committed, one per line, matched ignoring case. It lists real names, so
#              it lives in .git/info/forbidden and is never committed itself.
# Written for POSIX awk (no regex intervals), so it runs with macOS's awk, GNU awk and Git for Windows' awk.

BEGIN {
    file = (mode == "message") ? "commit message" : ""
    while (allowed != "" && (getline line < allowed) > 0) {
        if (line !~ /^[ \t]*(#|$)/) { sub(/[ \t]+$/, "", line); ok[line] = 1 }
    }
    nforbidden = 0
    while (forbidden != "" && (getline line < forbidden) > 0) {
        if (line !~ /^[ \t]*(#|$)/) { sub(/[ \t]+$/, "", line); bad[++nforbidden] = tolower(line) }
    }
    found = 0
}

function report(kind, value) {
    printf "%s:%d: %s: %s\n", file, lineno, kind, value
    found = 1
}

# Values that are plainly made up: tests of the secret redactor need key-shaped strings.
function fake(value) {
    return value ~ /abcdef|ABCDEF|012345|EXAMPLE|[Ee]xample|XXXX|xxxx/
}

# Addresses reserved for documentation (RFC 5737), loopback, and the generic private 10.0.0.0/24.
function reserved(a, b, c, d) {
    return (a == 127) || (a == 0 && b == 0 && c == 0 && d == 0) || (a == 255 && b == 255 && c == 255 && d == 255) \
        || (a == 192 && b == 0 && c == 2) || (a == 198 && b == 51 && c == 100) || (a == 203 && b == 0 && c == 113) \
        || (a == 10 && b == 0 && c == 0)
}

function check_addresses(text,    rest, value, parts, before) {
    rest = text
    while (match(rest, /[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+/)) {
        value = substr(rest, RSTART, RLENGTH)
        before = RSTART > 1 ? substr(rest, RSTART - 1, 1) : ""
        rest = substr(rest, RSTART + RLENGTH)
        # Part of a longer dotted number, such as a five-part version, isn't an address.
        if (before ~ /[0-9.]/ || rest ~ /^\.[0-9]/) continue
        split(value, parts, ".")
        if (parts[1] > 255 || parts[2] > 255 || parts[3] > 255 || parts[4] > 255) continue
        if (reserved(parts[1] + 0, parts[2] + 0, parts[3] + 0, parts[4] + 0) || (value in ok)) continue
        report("IP address (use 192.0.2.x, 198.51.100.x or 203.0.113.x, or add it to .githooks/allowed if it's public)", value)
    }
}

function check_pattern(text, pattern, minimum, kind,    rest, value) {
    rest = text
    while (match(rest, pattern)) {
        value = substr(rest, RSTART, RLENGTH)
        rest = substr(rest, RSTART + RLENGTH)
        if (RLENGTH >= minimum && !fake(value) && !(value in ok)) report(kind, value)
    }
}

# The body of a private key, certificate or similar: a long unbroken run of base64 that mixes digits with upper- and
# lowercase letters. Hexadecimal, as in the cryptography tests' published vectors, and paths in addresses don't count.
function random(value) {
    return value ~ /[0-9]/ && value ~ /[A-Z]/ && value ~ /[a-z]/ && value !~ /^[0-9a-fA-F]+$/
}

function check_base64(text,    rest, value) {
    rest = text
    while (match(rest, /[A-Za-z0-9+\/]+=*/)) {
        value = substr(rest, RSTART, RLENGTH)
        rest = substr(rest, RSTART + RLENGTH)
        if (RLENGTH >= 64 && random(value) && !fake(value) && !(value in ok)) report("long base64 (a private key or certificate?)", value)
    }
}

function check(text,    i, lower) {
    check_addresses(text)
    check_pattern(text, "sk-ant-[A-Za-z0-9_-]+", 30, "Anthropic API key")
    check_pattern(text, "sk-(proj-)?[A-Za-z0-9_-]+", 30, "OpenAI API key")
    check_pattern(text, "(gh[pousr]_|github_pat_)[A-Za-z0-9_]+", 30, "GitHub token")
    check_pattern(text, "AKIA[0-9A-Z]+", 20, "AWS access key")
    check_pattern(text, "xox[abpr]-[A-Za-z0-9-]+", 20, "Slack token")
    check_pattern(text, "AIza[0-9A-Za-z_-]+", 39, "Google API key")
    check_base64(text)
    lower = tolower(text)
    for (i = 1; i <= nforbidden; i++) {
        if (index(lower, bad[i]) > 0) report("forbidden in .git/info/forbidden", bad[i])
    }
}

mode == "message" {
    lineno = NR
    if ($0 !~ /^#/) check($0)
    next
}

# A staged diff with no context lines: follow the file and the new line numbers, and check only added lines.
/^\+\+\+ / { file = substr($0, 7); next }
/^@@ / {
    split($0, hunk, " ")
    sub(/^\+/, "", hunk[3]); split(hunk[3], start, ",")
    lineno = start[1] - 1
    next
}
/^\+/ {
    lineno++
    # Generated files that list versions and other people's contact details.
    if (file ~ /(^|\/)packages\.lock\.json$/ || file == "THIRD-PARTY-NOTICES.txt") next
    check(substr($0, 2))
}

END {
    if (found) {
        print ""
        print "Not committed: the lines above may hold a secret or a real host name or address. Use made-up names on reserved"
        print "domains (example.com, .test) and documentation addresses. If a value is fine to publish, add it to .githooks/allowed."
        exit 1
    }
}
