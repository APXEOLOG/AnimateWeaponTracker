# Animate Weapon Tracker

An ExileCore (ExileApi) plugin for Path of Exile that shows how many Animate Weapon minions you have out and when they expire.

The panel shows:
- two progress bars for the two weapons closest to expiring, with seconds left
- a stat line: current/max count, then how many weapons have under 10s left, under 50% of their duration left, and over 50% left

## How it works

The game keeps no per-weapon timer the overlay can read, so each weapon is timed from the moment it first appears in the Animate Weapon skill's deployed objects. Duration (`MinionDuration`) and the cap (`NumberOfAnimatedWeaponsAllowed`) come from the skill's stats.

- Weapons that were already out when the plugin started show as `?` until they are replaced.
- On an area change, weapons that follow you take over the previous timers (newest first).

## Settings

Panel position, bar width, the "expiring soon" threshold (10s), the middle band split (50%), hide when empty, and a debug window listing each weapon's id and remaining time.

## Building

Set `exapiPackage` to your ExileApi folder, or put this folder in `Plugins/Source` and let the overlay compile it.
