// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Utils;
using osu.Game.Rulesets.Taiko.UI;

namespace osu.Game.Rulesets.Taiko.Mods
{
    public partial class TaikoModForceAlternate : Mod, IApplicableToDrawableRuleset<TaikoHitObject>, IUpdatableByPlayfield
    {
        public override string Name => @"Force Alternate";
        public override string Acronym => @"FA";
        public override LocalisableString Description => @"Must alternate between left and right.";

        public override double ScoreMultiplier => 1.0;
        public override Type[] IncompatibleMods => new[] { typeof(ModAutoplay), typeof(ModRelax), typeof(TaikoModCinema) };
        public override ModType Type => ModType.Conversion;

        private DrawableTaikoRuleset ruleset = null!;

        private TaikoPlayfield playfield { get; set; } = null!;

        private bool bypass = false;
        private TaikoAction? lastAcceptedAction { get; set; }

        private PeriodTracker nonGameplayPeriods = null!;

        private IFrameStableClock gameplayClock = null!;

        public void ApplyToDrawableRuleset(DrawableRuleset<TaikoHitObject> drawableRuleset)
        {
            ruleset = (DrawableTaikoRuleset)drawableRuleset;
            ruleset.KeyBindingInputManager.Add(new InputInterceptor(this));
            playfield = (TaikoPlayfield)ruleset.Playfield;

            var periods = new List<Period>();

            if (drawableRuleset.Objects.Any())
            {
                periods.Add(new Period(int.MinValue, getValidJudgementTime(ruleset.Objects.First()) - 1));

                foreach (BreakPeriod b in drawableRuleset.Beatmap.Breaks)
                    periods.Add(new Period(b.StartTime, getValidJudgementTime(ruleset.Objects.First(h => h.StartTime >= b.EndTime)) - 1));

                static double getValidJudgementTime(HitObject hitObject) => hitObject.StartTime - hitObject.HitWindows.WindowFor(HitResult.Meh);
            }

            nonGameplayPeriods = new PeriodTracker(periods);

            gameplayClock = drawableRuleset.FrameStableClock;
        }

        public void Update(Playfield playfield)
        {
            if (!nonGameplayPeriods.IsInAny(gameplayClock.CurrentTime)) return;

            lastAcceptedAction = null;
        }

        private bool checkCorrectAction(TaikoAction action)
        {
            if (nonGameplayPeriods.IsInAny(gameplayClock.CurrentTime))
                return true;


            if (lastAcceptedAction == null)
            {
                lastAcceptedAction = action; return true;
            }
            if (playfield.HitObjectContainer.AliveObjects.FirstOrDefault(h => h.Result?.HasResult != true)?.HitObject is TaikoStrongableHitObject hitObject
                && hitObject.IsStrong
                && hitObject is not DrumRoll)
            { bypass = true; return true; }


            if (bypass == true)
            {
                bypass = false; lastAcceptedAction = action; return true;
            }
            if ((((int)lastAcceptedAction < 2) && (int)action > 1) || (((int)lastAcceptedAction > 1) && (int)action < 2))
            {
                lastAcceptedAction = action;
                return true;
            }
            return false;
        }

        private partial class InputInterceptor : Component, IKeyBindingHandler<TaikoAction>
        {
            private readonly TaikoModForceAlternate mod;

            public InputInterceptor(TaikoModForceAlternate mod)
            {
                this.mod = mod;
            }

            public bool OnPressed(KeyBindingPressEvent<TaikoAction> e)
                // if the pressed action is incorrect, block it from reaching gameplay.
                => !mod.checkCorrectAction(e.Action);

            public void OnReleased(KeyBindingReleaseEvent<TaikoAction> e)
            {
            }
        }
    }
}

