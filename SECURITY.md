# Security policy

KeyShot installs a global keyboard hook, so we take security reports seriously.

## What KeyShot guarantees

- It only **observes** key events. It never blocks, changes or injects input (`SendInput`/`keybd_event` are never called).
- It never records, logs, stores or transmits **which** characters you type. Diagnostics count key presses only.
- Its control pipe (`\\.\pipe\KeyShot.Control`) only accepts connections from the **current Windows user**.
- It makes no network connections.

A change that would break any of these is a security bug.

## Reporting a vulnerability

Please **don't open a public issue**. Report it privately through GitHub's
[private vulnerability reporting](https://github.com/prabindersinghh/keyshot-typing/security/advisories/new)
(the **Security** tab → **Report a vulnerability**).

Include the steps to reproduce, the impact, and the KeyShot version. You'll get an initial reply within 7 days.

## Supported versions

Only the latest release gets security fixes.
