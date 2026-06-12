using NiumaEffect.Enum;
using NiumaEffect.ViewData;
using NiumaUI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaEffect.Bridge
{
    public sealed class EffectToolkitReceiver : MonoBehaviour, IEffectUIReceiver
    {
        [SerializeField, Tooltip("拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。")]
        private UIToolkitUIManager uiManager;
        [SerializeField, Tooltip("效果面板 ViewId。默认 EffectPanel，需要在 UIToolkitViewRegistrySO 注册。")]
        private string effectViewId = "EffectPanel";
        [SerializeField] private bool autoOpenView = true;
        [SerializeField] private bool closeOnCleared = true;
        [SerializeField] private bool logWarnings = true;

        public void ApplyEffectUpdate(EffectUIUpdate update)
        {
            if (update.UpdateType == EffectUIUpdateType.Cleared && closeOnCleared && uiManager != null) uiManager.CloseView(effectViewId);
            if (!EnsureUIManager()) return;
            var refreshed = uiManager.RefreshView(effectViewId, update);
            if (!refreshed && autoOpenView) refreshed = uiManager.OpenView(effectViewId, update);
            if (!refreshed) Warn($"没有刷新到效果 Toolkit View：ViewId={effectViewId}。请检查 Registry 和 BindingProvider。");
        }

        private bool EnsureUIManager()
        {
            if (uiManager == null) uiManager = FindSceneObject<UIToolkitUIManager>();
            if (uiManager != null) return true;
            Warn("未绑定 UIToolkitUIManager，效果 Toolkit 面板无法刷新。");
            return false;
        }

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message)) UnityEngine.Debug.LogWarning($"[EffectToolkitReceiver] {message}", this);
        }

        private static T FindSceneObject<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }

    public sealed class EffectToolkitBindingProvider : MonoBehaviour, IToolkitViewBindingProvider
    {
        [SerializeField, Tooltip("BindingProviderId，默认 EffectPanel。需要和 Registry 一致。")]
        private string providerId = "EffectPanel";
        [SerializeField] private string titleLabelName = "TitleText";
        [SerializeField] private string statusLabelName = "StatusText";
        [SerializeField] private string listRootName = "ListRoot";
        [SerializeField] private string detailLabelName = "DetailText";
        [SerializeField] private string resultLabelName = "ResultText";
        [SerializeField] private string emptyRootName = "EmptyRoot";
        [SerializeField] private int maxRows = 60;
        [SerializeField] private string rowClass = "niuma-effect-row";

        public string ProviderId => string.IsNullOrWhiteSpace(providerId) ? "EffectPanel" : providerId.Trim();
        public IToolkitViewBinding CreateBinding() => new EffectToolkitBinding(titleLabelName, statusLabelName, listRootName, detailLabelName, resultLabelName, emptyRootName, maxRows, rowClass);
    }

    public sealed class EffectToolkitBinding : ToolkitViewBindingBase
    {
        private readonly string _titleName, _statusName, _listName, _detailName, _resultName, _emptyName, _rowClass;
        private readonly int _maxRows;
        private Label _title, _status, _detail, _result;
        private VisualElement _list, _empty;

        public EffectToolkitBinding(string titleName, string statusName, string listName, string detailName, string resultName, string emptyName, int maxRows, string rowClass)
        {
            _titleName = titleName; _statusName = statusName; _listName = listName; _detailName = detailName; _resultName = resultName; _emptyName = emptyName;
            _maxRows = Mathf.Max(1, maxRows);
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-effect-row" : rowClass.Trim();
        }

        protected override void OnInitialize()
        {
            _title = QL(_titleName); _status = QL(_statusName); _list = QE(_listName); _detail = QL(_detailName); _result = QL(_resultName); _empty = QE(_emptyName);
            Apply(null, EffectUIUpdateType.Cleared, 0);
        }

        protected override void OnRefresh(object viewData)
        {
            if (viewData is EffectUIUpdate update) Apply(update.Current, update.UpdateType, update.Revision);
            else Apply(null, EffectUIUpdateType.Cleared, 0);
        }

        protected override void OnClose() => Apply(null, EffectUIUpdateType.Cleared, 0);

        private void Apply(EffectPanelViewData panel, EffectUIUpdateType updateType, long revision)
        {
            Clear();
            var effects = panel?.Effects ?? System.Array.Empty<EffectIconViewData>();
            Set(_title, "效果");
            SetVisible(_empty, panel == null || effects.Length == 0);
            Set(_status, panel == null ? $"状态：{updateType}" : $"Actor {Text(panel.ActorId, "未知")} | Revision {panel.Revision} | 效果 {effects.Length}");
            Set(_detail, panel == null ? "暂无效果数据。" : "Buff / Debuff / 状态效果由 NiumaEffect 推送。UI 只显示，不修改生命周期。");
            Set(_result, string.Empty);

            for (var i = 0; i < effects.Length && i < _maxRows; i++)
            {
                var e = effects[i];
                if (e == null) continue;
                var time = e.IsPermanent ? "永久" : $"{e.RemainingSeconds:0.0}/{e.DurationSeconds:0.0}s";
                Add($"{Text(e.DisplayName, e.EffectId)} x{e.StackCount} | {e.Type}/{e.Polarity} | {time}{(e.IsDispellable ? " | 可驱散" : " | 不可驱散")}{(e.IsMissingDefinition ? " | 缺失定义" : string.Empty)}");
            }
        }

        private Label QL(string name) => string.IsNullOrWhiteSpace(name) ? null : Query<Label>(name.Trim());
        private VisualElement QE(string name) => string.IsNullOrWhiteSpace(name) ? null : Root?.Q<VisualElement>(name.Trim());
        private void Clear() { if (_list != null) _list.Clear(); }
        private void Add(string text) { if (_list == null) return; var row = new Label(text ?? string.Empty); row.AddToClassList(_rowClass); _list.Add(row); }
        private static void Set(Label label, string text) { if (label != null) label.text = text ?? string.Empty; }
        private static void SetVisible(VisualElement element, bool visible) { if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }
        private static string Text(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }
}
