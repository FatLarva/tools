using System;
using System.Linq;
using Tools.StructuredProcess.Declarative;
using UnityEditor;
using UnityEngine;

namespace Tools.StructuredProcess.Editor
{
    public sealed class StructuredProcessTraceWindow : EditorWindow
    {
        private const float RowHeight = 23f;
        private const float StatusWidth = 82f;
        private const float DurationWidth = 82f;
        private const float WorkerBadgeWidth = 28f;
        private const float EventTimeWidth = 108f;
        private static readonly Color WaitingColor = new Color(0.44f, 0.46f, 0.49f);
        private static readonly Color ReadyColor = new Color(0.72f, 0.55f, 0.26f);
        private static readonly Color RunningColor = new Color(0.25f, 0.52f, 0.78f);
        private static readonly Color SucceededColor = new Color(0.25f, 0.64f, 0.43f);
        private static readonly Color FailedColor = new Color(0.79f, 0.32f, 0.31f);
        private static readonly Color WorkerColor = new Color(0.72f, 0.54f, 0.94f);
        private static readonly GUIContent WaitLegend = new GUIContent("Wait", "Waiting for the executor to observe all dependency completions.");
        private static readonly GUIContent ReadyLegend = new GUIContent("Ready", "Dependencies are satisfied. The step has yet to launch.");
        private static readonly GUIContent RunLegend = new GUIContent("Run", "From launch through completion, including input binding and worker queue time.");
        private static readonly GUIContent WorkerBadge = new GUIContent("BG",
            "Background dispatch. The parallel executor schedules construction and ExecuteAsync on the thread pool.");

        private ProcessTrace _trace;
        private ProcessTrace.Snapshot _snapshot;
        private StepRow[] _rows = Array.Empty<StepRow>();
        private Vector2 _scroll;
        private Vector2 _detailsScroll;
        private int _selectedStep = -1;

        public static void ShowTrace(ProcessTrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            var window = Open();
            window.SetTrace(trace);
        }

        [MenuItem("Window/Tools/Structured Process Trace")]
        private static void ShowWindow()
        {
            Open();
        }

        private static StructuredProcessTraceWindow Open()
        {
            var window = GetWindow<StructuredProcessTraceWindow>("Process Trace");
            window.minSize = new Vector2(570f, 300f);
            return window;
        }

        private void SetTrace(ProcessTrace trace)
        {
            _trace = trace;
            _snapshot = default;
            _selectedStep = -1;
            _scroll = Vector2.zero;
            _detailsScroll = Vector2.zero;
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (_trace != null && _snapshot.Status <= ProcessTraceStatus.Running)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout && _trace != null && _snapshot.Status <= ProcessTraceStatus.Running)
            {
                RefreshSnapshot();
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Structured Process Trace", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_trace == null))
                {
                    if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                    {
                        SetTrace(null);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (_trace == null || _snapshot.ProcessType == null)
            {
                EditorGUILayout.HelpBox("Run the 20-step match example or call ShowTrace with a ProcessTrace from your code.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(_snapshot.ProcessType.Name, $"{_snapshot.Status}  ·  {FormatTime(_snapshot.Elapsed)}", EditorStyles.boldLabel);
            var errorRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            if (_snapshot.Error != null)
            {
                var content = new GUIContent(_snapshot.Error, _snapshot.Exception?.ToString() ?? _snapshot.Error);
                EditorGUI.LabelField(errorRect, content);
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                DrawHeader();
                for (int index = 0; index < _rows.Length; index++)
                {
                    DrawStep(index);
                }
            }

            if (_selectedStep >= 0)
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(_detailsScroll,
                    GUILayout.Height(Mathf.Min(184f, position.height * 0.36f))))
                {
                    _detailsScroll = scroll.scrollPosition;
                    DrawDetails(_selectedStep);
                }
            }
        }

        private void RefreshSnapshot()
        {
            _snapshot = _trace.GetSnapshot();
            _rows = new StepRow[_snapshot.Steps.Count];
            foreach (var recorded in _snapshot.Events)
            {
                ref var row = ref _rows[recorded.StepIndex];
                row.Latest = recorded;
                if (recorded.Status == ProcessTraceStepStatus.Ready)
                {
                    row.Ready = recorded.Elapsed;
                }
                else if (recorded.Status == ProcessTraceStepStatus.Running)
                {
                    row.Started = recorded.Elapsed;
                }
            }
        }

        private void DrawHeader()
        {
            var rect = EditorGUILayout.GetControlRect(false, RowHeight);
            SplitRow(rect, out var step, out var status, out var duration, out var timeline);
            EditorGUI.LabelField(step, "Step", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(status, "State", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(duration, "Run time", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(timeline, "Elapsed", EditorStyles.miniBoldLabel);
            timeline.xMin += 53f;
            DrawLegend(timeline, WaitLegend, WaitingColor, 0);
            DrawLegend(timeline, ReadyLegend, ReadyColor, 1);
            DrawLegend(timeline, RunLegend, RunningColor, 2);
        }

        private void DrawStep(int index)
        {
            var rect = EditorGUILayout.GetControlRect(false, RowHeight);
            SplitRow(rect, out var step, out var status, out var duration, out var timeline);
            var info = _snapshot.Steps[index];
            var row = _rows[index];
            var latest = row.Latest;
            bool usesWorker = UsesWorkerThread(info);
            if (usesWorker)
            {
                var badge = new Rect(step.xMax - WorkerBadgeWidth, step.y, WorkerBadgeWidth, step.height);
                step.xMax = badge.xMin - 4f;
                EditorGUI.DrawRect(new Rect(badge.x, badge.y + 5f, 2f, badge.height - 10f), WorkerColor);
                badge.xMin += 6f;
                EditorGUI.LabelField(badge, WorkerBadge, EditorStyles.miniBoldLabel);
            }

            var label = new GUIContent(DisplayName(info), info.Id);
            bool selected = _selectedStep == index;
            if (GUI.Toggle(step, selected, label, EditorStyles.miniButton) != selected)
            {
                _selectedStep = selected ? -1 : index;
                _detailsScroll = Vector2.zero;
                GUIUtility.ExitGUI();
            }

            EditorGUI.LabelField(status, latest.Status.ToString());
            TimeSpan end = EndOfStep(row);
            TimeSpan? runTime = row.Started.HasValue ? end - row.Started.Value : null;

            EditorGUI.LabelField(duration, runTime.HasValue ? FormatTime(runTime.Value) : "—");
            timeline.yMin += 5f;
            timeline.yMax -= 5f;
            EditorGUI.DrawRect(timeline, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.76f, 0.76f, 0.76f));
            DrawSpan(timeline, TimeSpan.Zero, row.Ready ?? end, WaitingColor);
            if (row.Ready.HasValue)
            {
                DrawSpan(timeline, row.Ready.Value, row.Started ?? end, ReadyColor);
            }

            if (row.Started.HasValue)
            {
                var color = latest.Status == ProcessTraceStepStatus.Failed ? FailedColor
                    : latest.Status == ProcessTraceStepStatus.Succeeded ? SucceededColor : RunningColor;
                DrawSpan(timeline, row.Started.Value, end, color, true);
                if (usesWorker)
                {
                    timeline.yMin = timeline.yMax - 2f;
                    DrawSpan(timeline, row.Started.Value, end, WorkerColor, true);
                }
            }
        }

        private void DrawDetails(int index)
        {
            var step = _snapshot.Steps[index];
            var row = _rows[index];
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(step.Id, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Policy", $"{(step.Optional ? "Optional" : "Required")} · {step.Thread} thread");
                EditorGUILayout.LabelField("Dispatch", UsesWorkerThread(step) ? "Thread pool" : "Calling thread");
                string dependencies = step.Dependencies.Count == 0 ? "None"
                    : string.Join(", ", step.Dependencies.Select(dependency => DisplayName(_snapshot.Steps[dependency])));
                EditorGUILayout.LabelField("Depends on", dependencies, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(4f);
                DrawEventRow("Event", "Since run start", "Duration", EditorStyles.miniBoldLabel);
                foreach (var recorded in _snapshot.Events)
                {
                    if (recorded.StepIndex == index)
                    {
                        var duration = PhaseDuration(recorded, row);
                        DrawEventRow(recorded.Status.ToString(), FormatTime(recorded.Elapsed),
                            duration.HasValue ? FormatTime(duration.Value) : "—", EditorStyles.label);
                        if (recorded.Error != null)
                        {
                            EditorGUILayout.HelpBox(recorded.Error,
                                recorded.Status == ProcessTraceStepStatus.Failed ? MessageType.Error : MessageType.Info);
                        }
                    }
                }
            }
        }

        private static void DrawEventRow(string phase, string timestamp, string duration, GUIStyle style)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(phase, style, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField(timestamp, style, GUILayout.Width(EventTimeWidth));
                EditorGUILayout.LabelField(duration, style, GUILayout.Width(EventTimeWidth));
            }
        }

        private TimeSpan? PhaseDuration(ProcessTrace.Entry entry, StepRow row)
        {
            var end = EndOfStep(row);
            return entry.Status switch
            {
                ProcessTraceStepStatus.Waiting => (row.Ready ?? end) - entry.Elapsed,
                ProcessTraceStepStatus.Ready => (row.Started ?? end) - entry.Elapsed,
                ProcessTraceStepStatus.Running => end - entry.Elapsed,
                _ => null,
            };
        }

        private TimeSpan EndOfStep(StepRow row)
        {
            return row.Latest.Status <= ProcessTraceStepStatus.Running ? _snapshot.Elapsed : row.Latest.Elapsed;
        }

        private static void SplitRow(Rect rect, out Rect step, out Rect status, out Rect duration, out Rect timeline)
        {
            float stepWidth = Mathf.Max(147f, rect.width * 0.30f);
            step = new Rect(rect.x, rect.y, stepWidth - 6f, rect.height);
            status = new Rect(rect.x + stepWidth, rect.y, StatusWidth, rect.height);
            duration = new Rect(status.xMax, rect.y, DurationWidth, rect.height);
            timeline = new Rect(duration.xMax, rect.y, Mathf.Max(0f, rect.xMax - duration.xMax), rect.height);
        }

        private static void DrawLegend(Rect rect, GUIContent label, Color color, int index)
        {
            float x = rect.x + index * 45f;
            EditorGUI.DrawRect(new Rect(x, rect.y + 8f, 6f, 6f), color);
            EditorGUI.LabelField(new Rect(x + 9f, rect.y, 40f, rect.height), label, EditorStyles.miniLabel);
        }

        private void DrawSpan(Rect rect, TimeSpan start, TimeSpan end, Color color, bool showShortStep = false)
        {
            double duration = Math.Max(1.0, _snapshot.Elapsed.Ticks);
            float from = rect.width * Mathf.Clamp01((float)(start.Ticks / duration));
            float to = rect.width * Mathf.Clamp01((float)(end.Ticks / duration));
            float width = Mathf.Min(rect.width - from, Mathf.Max(showShortStep ? 2f : 0f, to - from));
            if (width > 0f)
            {
                EditorGUI.DrawRect(new Rect(rect.x + from, rect.y, width, rect.height), color);
            }
        }

        private static string FormatTime(TimeSpan time)
        {
            return $"{time.TotalMilliseconds:F1} ms";
        }

        private bool UsesWorkerThread(ProcessTrace.Step step)
        {
            return step.Thread == StepThread.Any && typeof(ParallelProcess).IsAssignableFrom(_snapshot.ProcessType);
        }

        private static string DisplayName(ProcessTrace.Step step)
        {
            string namespaceName = step.Type.Namespace;
            if (!string.IsNullOrEmpty(namespaceName) && step.Id.StartsWith(namespaceName + ".", StringComparison.Ordinal))
            {
                return step.Id.Substring(namespaceName.Length + 1);
            }

            return step.Id;
        }

        private struct StepRow
        {
            public ProcessTrace.Entry Latest;
            public TimeSpan? Ready;
            public TimeSpan? Started;
        }
    }
}
