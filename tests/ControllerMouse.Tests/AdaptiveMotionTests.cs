using ControllerMouse.Core;

internal static class AdaptiveMotionTests
{
    public static void Run(Action<string, Action> test)
    {
        static void Check(bool condition) { if (!condition) throw new Exception("Adaptive motion assertion failed."); }
        static AdaptiveMotion Train(double dt, bool jitter, double seconds = 140)
        {
            var learner = new AdaptiveMotion();
            for (double t = 0; t < seconds; t += dt)
                learner.Observe(.22, jitter ? .17 * Math.Sin(t * 2 * Math.PI * 8) : 0, dt, LearningMode.Learn, false);
            learner.Observe(0, 0, dt, LearningMode.Learn, false);
            return learner;
        }
        test("adaptive learning warms up and commits only at neutral", () =>
        {
            var learner = Train(.008, true, 4); Check(learner.Model.Smoothing == 0);
            for (double t = 0; t < 80; t += .008) learner.Observe(.22, .17 * Math.Sin(t * 50), .008, LearningMode.Learn, false);
            Check(learner.Model.Smoothing == 0);
            learner.Observe(0, 0, .008, LearningMode.Learn, false); Check(learner.Model.Smoothing > 0 && learner.Model.Smoothing <= .04);
        });
        test("steady motion stays unchanged and variable motion learns bounded help across polling rates", () =>
        {
            Check(Train(.008, false).Model.Smoothing == 0);
            var fast = Train(.008, true); var slow = Train(.02, true);
            Check(fast.Model.Smoothing > .005 && Math.Abs(fast.Model.Smoothing - slow.Model.Smoothing) < .01);
            var adjustment = fast.Adjustment(LearningMode.Learn, .2, false);
            Check(adjustment.Smoothing <= .04 && adjustment.Speed is >= .85 and <= 1);
        });
        test("off frozen dragging large motion and invalid intervals cannot learn or apply unwanted help", () =>
        {
            var learner = Train(.008, true); var saved = learner.Model;
            for (int i = 0; i < 1000; i++)
            {
                learner.Observe(.2, -.2, .008, LearningMode.Frozen, false);
                learner.Observe(.2, -.2, .008, LearningMode.Off, false);
                learner.Observe(.2, -.2, .008, LearningMode.Learn, true);
                learner.Observe(.2, -.2, double.NaN, LearningMode.Learn, false);
                learner.Observe(1, 0, .008, LearningMode.Learn, false);
            }
            Check(learner.Model == saved);
            Check(learner.Adjustment(LearningMode.Off, .2, false) == (0d, 1d));
            Check(learner.Adjustment(LearningMode.Frozen, .2, false).Smoothing > 0);
            Check(learner.Adjustment(LearningMode.Learn, 1, false) == (0d, 1d));
            Check(learner.Adjustment(LearningMode.Learn, .2, true) == (0d, 1d));
            learner.Forget(); Check(learner.Model == new MotionModel());
        });
        test("short repeated gestures accumulate evidence without learning from rest", () =>
        {
            var learner = new AdaptiveMotion();
            for (int gesture = 0; gesture < 100; gesture++)
            {
                for (double t = 0; t < 1; t += .01) learner.Observe(.22, .17 * Math.Sin(t * 50), .01, LearningMode.Learn, false);
                learner.Observe(0, 0, .01, LearningMode.Learn, false);
            }
            Check(learner.Model.Seconds > 60 && learner.Model.Smoothing > 0);
            var before = learner.Model;
            for (int i = 0; i < 10000; i++) learner.Observe(0, 0, .01, LearningMode.Learn, false);
            Check(learner.Model == before);
        });
        test("learned smoothing reduces synthetic transverse wobble in actual pointer output", () =>
        {
            double Travel(LearningMode mode)
            {
                var sink = new Recorder(); var engine = new InputProcessor(sink) { Settings = new Settings { MotionLearning = mode, Smoothing = 0 } };
                engine.MotionLearning.Load(Train(.008, true).Model); engine.Process(default(Gamepad), .008);
                double previous = 0, distance = 0;
                for (double t = 0; t < 10; t += .008)
                {
                    engine.Process(new(Buttons.None, 0, 0, 13000, (short)(6000 * Math.Sin(t * 50)), 0, 0), .008);
                    distance += Math.Abs(sink.Y - previous); previous = sink.Y;
                }
                return distance;
            }
            var baseline = Travel(LearningMode.Off); var adapted = Travel(LearningMode.Frozen);
            Check(baseline > 0 && adapted < baseline * .85);
        });
        test("clicks and fast motion preserve brief correction evidence instead of starving learning", () =>
        {
            var learner = new AdaptiveMotion();
            for (int gesture = 0; gesture < 160; gesture++)
            {
                for (double t = 0; t < .1; t += .008) learner.Observe(.22, .17 * Math.Sin((t + gesture * .1) * 50), .008, LearningMode.Learn, false);
                learner.Observe(.22, 0, .008, LearningMode.Learn, true);
                learner.Observe(1, 0, .008, LearningMode.Learn, false);
                learner.Observe(0, 0, .008, LearningMode.Learn, false);
            }
            Check(learner.Model.Seconds > 10 && learner.Model.Smoothing > 0);
        });
        test("motion memory persists isolated profiles and forgets deleted profile statistics", () =>
        {
            string folder = Path.Combine(Path.GetTempPath(), "relay-motion-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "motion.json"); var store = new MotionLearningStore(path);
                var trained = Train(.008, true).Model; store.Put("a", trained); store.Put("b", new()); store.Save();
                var loaded = new MotionLearningStore(path); loaded.Load();
                Check(loaded.Get("a") == trained && loaded.Get("b") == new MotionModel());
                loaded.Retain(["b"]); loaded.Save(); var again = new MotionLearningStore(path); again.Load(); Check(again.Get("a") == new MotionModel());
                File.WriteAllText(path, "{broken"); again.Load(); Check(again.Warning != null);
            }
            finally { Directory.Delete(folder, true); }
        });
        test("adaptive processor still stops immediately at neutral and releases on pause", () =>
        {
            var sink = new Recorder(); var engine = new InputProcessor(sink) { Settings = new Settings { MotionLearning = LearningMode.Frozen } };
            engine.MotionLearning.Load(new(100, .5, .04)); engine.Process(default(Gamepad), .008);
            for (int i = 0; i < 50; i++) engine.Process(new(Buttons.None, 0, 0, 15000, 0, 0, 0), .008);
            var moved = sink.X; engine.Process(default(Gamepad), .008); Check(sink.X == moved);
            engine.Process(new(Buttons.A, 0, 0, 0, 0, 0, 0), .008); engine.SetEnabled(false);
            Check(sink.Events.Last() == "Left:False");
            var before = engine.MotionLearning.Model;
            for (int i = 0; i < 1000; i++) engine.Process(new(Buttons.None, 0, 0, 15000, 0, 0, 0), .008);
            Check(engine.MotionLearning.Model == before && sink.X == moved);
        });
    }
}
