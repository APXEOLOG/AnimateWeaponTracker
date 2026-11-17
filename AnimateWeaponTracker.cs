using System.Diagnostics;
using System.Numerics;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using Hexa.NET.ImGui;
using Color = SharpDX.Color;

namespace AnimateWeaponTracker;

/// <summary>
/// Shows how many Animate Weapon minions are out and when they expire.
/// The game keeps no per-weapon timer that the overlay can read, so each weapon is timed
/// from the moment its entity first shows up in the skill's deployed objects.
/// </summary>
public class AnimateWeaponTracker : BaseSettingsPlugin<AnimateWeaponTrackerSettings>
{
    // Weapons carried into a new area keep their remaining time if they show up within this window.
    private const double CarryOverWindowSeconds = 15;

    private readonly Stopwatch _clock = Stopwatch.StartNew();

    // Entity id -> time the weapon was first seen (null = it was already out before tracking started).
    private readonly Dictionary<long, double?> _startTimes = new();

    private bool _hasBaseline;
    private double _areaChangedAt = double.NegativeInfinity;
    private List<double?> _carryOver = [];

    private int _alive;
    private int _max;
    private double _durationSeconds;
    private List<string> _skillNames = [];

    private double Now => _clock.Elapsed.TotalSeconds;

    public override void AreaChange(AreaInstance area)
    {
        // Entities get new ids in the new area. Keep the start times so the weapons that follow
        // the player can take them over: newest first, since the oldest are the ones that may
        // have run out during the load.
        _carryOver = _startTimes.Values
            .OrderBy(s => s ?? double.NegativeInfinity)
            .ToList();
        _startTimes.Clear();
        _areaChangedAt = Now;
    }

    public override void Render()
    {
        if (!GameController.InGame || GameController.Game.IsEscapeState)
        {
            return;
        }

        var weapons = Update();
        if (weapons == null)
        {
            return;
        }

        if (Settings.ShowDebug)
        {
            DrawDebug(weapons);
        }

        if (_alive == 0 && Settings.HideWhenEmpty)
        {
            return;
        }

        DrawPanel(weapons);
    }

    private List<(long Id, double? Remaining)> Update()
    {
        var player = GameController.Player;
        if (player == null || !player.TryGetComponent<Actor>(out var actor))
        {
            return null;
        }

        var skills = actor.ActorSkills?
            .Where(s => s.Name?.StartsWith("AnimateWeapon", StringComparison.Ordinal) == true)
            .ToList() ?? [];
        _skillNames = skills.Select(s => s.Name).ToList();

        var durationMs = 0;
        var max = 0;
        var entities = new Dictionary<long, Entity>();
        foreach (var skill in skills)
        {
            var stats = skill.Stats;
            if (stats != null)
            {
                var d = stats.GetValueOrDefault(GameStat.MinionDuration);
                if (d <= 0) d = stats.GetValueOrDefault(GameStat.SkillEffectDuration);
                durationMs = Math.Max(durationMs, d);
                max = Math.Max(max, stats.GetValueOrDefault(GameStat.NumberOfAnimatedWeaponsAllowed));
            }

            foreach (var deployed in skill.DeployedObjects ?? [])
            {
                var e = deployed?.Entity;
                if (e is { IsValid: true, IsAlive: true })
                {
                    entities.TryAdd(e.Id, e);
                }
            }
        }

        _durationSeconds = durationMs / 1000.0;
        _max = max;
        _alive = entities.Count;

        var now = Now;
        foreach (var id in _startTimes.Keys.Where(id => !entities.ContainsKey(id)).ToList())
        {
            _startTimes.Remove(id);
        }

        foreach (var id in entities.Keys.Where(id => !_startTimes.ContainsKey(id)).OrderBy(id => id))
        {
            if (!_hasBaseline)
            {
                // Plugin just started: these were summoned before we were watching.
                _startTimes[id] = null;
            }
            else if (_carryOver.Count > 0 && now - _areaChangedAt <= CarryOverWindowSeconds)
            {
                _startTimes[id] = _carryOver[^1];
                _carryOver.RemoveAt(_carryOver.Count - 1);
            }
            else
            {
                _startTimes[id] = now;
            }
        }

        _hasBaseline = true;
        if (now - _areaChangedAt > CarryOverWindowSeconds)
        {
            _carryOver.Clear();
        }

        return _startTimes
            .Select(kv => (kv.Key, kv.Value is { } start ? _durationSeconds - (now - start) : (double?)null))
            .ToList();
    }

    private void DrawPanel(List<(long Id, double? Remaining)> weapons)
    {
        var warn = Settings.WarningSeconds.Value;
        var midPct = Settings.MiddleBandPercent.Value;
        var midLimit = _durationSeconds * midPct / 100.0;

        var known = weapons.Where(w => w.Remaining.HasValue).Select(w => Math.Max(0, w.Remaining!.Value)).OrderBy(r => r).ToList();
        var urgent = known.Count(r => r < warn);
        var middle = known.Count(r => r >= warn && r < midLimit);
        var high = known.Count(r => r >= warn && r >= midLimit);
        var unknown = weapons.Count - known.Count;

        var red = new Color(240, 110, 110);
        var amber = new Color(250, 199, 117);
        var green = new Color(151, 196, 89);
        var text = new Color(230, 228, 220);
        var dim = new Color(136, 135, 128);
        Color BandColor(double r) => r < warn ? red : r < midLimit ? amber : green;

        // Stat line: count, then how many weapons are in each band.
        var countColor = _max > 0 && _alive >= _max ? green : _alive * 2 < _max ? red : amber;
        var segments = new List<(string Text, Color Color)>
        {
            ($"{_alive}/{(_max > 0 ? _max : "?")}", countColor),
            ($"<{warn}s: {urgent}", urgent > 0 ? red : dim),
            ($"<{midPct}%: {middle}", middle > 0 ? amber : dim),
            ($">{midPct}%: {high}", high > 0 ? text : dim),
        };
        if (unknown > 0)
        {
            segments.Add(($"?: {unknown}", dim));
        }

        const float pad = 6;
        const float gap = 4;
        var spacing = Graphics.MeasureText("   ").X;
        var segmentSizes = segments.Select(seg => Graphics.MeasureText(seg.Text)).ToList();
        var statWidth = segmentSizes.Sum(sz => sz.X) + spacing * (segments.Count - 1);
        var lineHeight = segmentSizes.Max(sz => sz.Y);
        var barHeight = lineHeight + 4;
        var width = Math.Max(Settings.BarWidth.Value, statWidth);

        var window = GameController.Window.GetWindowRectangle();
        var pos = new Vector2(window.Width * Settings.PositionX.Value / 100f, window.Height * Settings.PositionY.Value / 100f);
        var height = pad * 2 + (barHeight + gap) * 2 + lineHeight;
        Graphics.DrawBox(pos, pos + new Vector2(width + pad * 2, height), new Color(0, 0, 0, 170));

        // Two bars for the weapons closest to expiring; an empty bar keeps the layout steady.
        var y = pos.Y + pad;
        for (var i = 0; i < 2; i++)
        {
            var barPos = new Vector2(pos.X + pad, y);
            Graphics.DrawBox(barPos, barPos + new Vector2(width, barHeight), new Color(68, 68, 65, 220));
            var label = "-";
            if (i < known.Count)
            {
                var r = known[i];
                var fraction = _durationSeconds > 0 ? (float)Math.Clamp(r / _durationSeconds, 0, 1) : 0;
                Graphics.DrawBox(barPos, barPos + new Vector2(width * fraction, barHeight), BandColor(r));
                label = $"{r:0.0}s";
            }

            var labelSize = Graphics.MeasureText(label);
            Graphics.DrawText(label, barPos + new Vector2((width - labelSize.X) / 2, (barHeight - labelSize.Y) / 2), Color.White);
            y += barHeight + gap;
        }

        var x = pos.X + pad;
        for (var i = 0; i < segments.Count; i++)
        {
            Graphics.DrawText(segments[i].Text, new Vector2(x, y), segments[i].Color);
            x += segmentSizes[i].X + spacing;
        }
    }

    private void DrawDebug(List<(long Id, double? Remaining)> weapons)
    {
        var show = Settings.ShowDebug.Value;
        if (ImGui.Begin("Animate Weapon Tracker debug", ref show))
        {
            ImGui.Text($"Skills: {string.Join(", ", _skillNames)}");
            ImGui.Text($"Alive {_alive}, max {_max}, duration {_durationSeconds:0.0}s");
            ImGui.Text($"Carry-over pending: {_carryOver.Count}, since area change: {Now - _areaChangedAt:0.0}s");
            foreach (var (id, remaining) in weapons.OrderBy(w => w.Remaining ?? double.MaxValue))
            {
                ImGui.Text($"  {id}: {(remaining is { } r ? $"{r:0.0}s" : "unknown")}");
            }
        }

        ImGui.End();
        Settings.ShowDebug.Value = show;
    }
}
