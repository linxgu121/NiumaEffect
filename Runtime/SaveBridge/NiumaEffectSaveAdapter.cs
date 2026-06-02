using System;
using System.Collections.Generic;
using System.Text;
using NiumaEffect.Controller;
using NiumaEffect.Data;
using NiumaEffect.Enum;
using NiumaSave.Controller;
using NiumaSave.Data;
using NiumaSave.Provider;
using UnityEngine;

namespace NiumaEffect.SaveBridge
{
    /// <summary>
    /// NiumaEffect 存档桥接器。
    /// 负责把效果运行时快照转换为 NiumaSave 的 Section 数据，并在读档时恢复到效果控制器。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaEffectSaveAdapter : MonoBehaviour, ISaveDataProvider
    {
        private const string EffectSectionId = "effect";
        private const string EffectSectionVersionV1 = "1";
        private const string CurrentEffectSectionVersion = EffectSectionVersionV1;
        private const string EffectSectionFormat = "json";

        [Header("模块引用")]
        [Tooltip("效果模块根控制器。请拖入场景中的 NiumaEffectController，导出和导入效果状态都会通过它完成。")]
        [SerializeField] private NiumaEffectController effectController;

        [Tooltip("存档模块根控制器。开启自动注册时，请拖入场景中的 NiumaSaveController。")]
        [SerializeField] private NiumaSaveController saveController;

        [Header("注册行为")]
        [Tooltip("启用组件时是否自动注册到 NiumaSaveController。正式场景建议开启，并确保 NiumaSaveController 更早初始化，或把本组件挂在存档控制器子物体下。")]
        [SerializeField] private bool registerOnEnable = true;

        [Tooltip("引用为空时是否自动在场景中查找对应组件。仅建议调试阶段开启；正式多场景或全局场景必须手动绑定，避免找到错误实例。")]
        [SerializeField] private bool autoFindReferences = true;

        private bool _registeredToSaveController;

        /// <summary>
        /// 效果模块的稳定存档段 ID。
        /// </summary>
        public string SectionId => EffectSectionId;

        /// <summary>
        /// 效果存档段结构版本。
        /// </summary>
        public string SectionVersion => CurrentEffectSectionVersion;

        /// <summary>
        /// 效果模块数据修订号。
        /// NiumaSave 通过该值判断效果模块是否发生变化。
        /// </summary>
        public long Revision => effectController != null ? effectController.EffectRevision : 0L;

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            if (registerOnEnable)
            {
                RegisterToSaveController();
            }
        }

        private void OnDisable()
        {
            UnregisterFromSaveController();
        }

        /// <summary>
        /// 导出效果运行时快照为 NiumaSave Section。
        /// SaveDataProviderRegistry 会捕获该方法抛出的异常并转为结构化导出失败；直接调用时必须自行处理 InvalidOperationException。
        /// </summary>
        public SaveSectionData ExportSection()
        {
            ResolveReferences(false);
            if (effectController == null)
            {
                throw new InvalidOperationException("NiumaEffectSaveAdapter 缺少 NiumaEffectController，无法导出效果存档。");
            }

            if (!effectController.IsInitialized)
            {
                throw new InvalidOperationException("NiumaEffectController 尚未初始化，拒绝导出空效果存档以避免覆盖有效数据。");
            }

            var saveData = effectController.ExportSnapshot();
            ValidateSaveDataForExport(saveData);

            var json = JsonUtility.ToJson(saveData);
            var bytes = Encoding.UTF8.GetBytes(json);

            return new SaveSectionData
            {
                SectionId = SectionId,
                SectionVersion = SectionVersion,
                Format = EffectSectionFormat,
                DataEncoding = SaveDataEncoding.Base64,
                EncodedData = Convert.ToBase64String(bytes)
            };
        }

        /// <summary>
        /// 从 NiumaSave Section 导入效果快照。
        /// 导入前会先完成结构校验；损坏或空数据不会清空当前运行中的效果。
        /// </summary>
        public SaveSectionImportResult ImportSection(SaveSectionData section)
        {
            ResolveReferences(false);
            if (effectController == null)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.ConfigMissing,
                    "NiumaEffectSaveAdapter 缺少 NiumaEffectController，无法导入效果存档。");
            }

            if (section == null)
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.NullSection, "效果存档段为空。");
            }

            if (!string.Equals(section.SectionId, SectionId, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.SectionIdMismatch,
                    $"效果存档段 ID 不匹配：expected={SectionId}, actual={section.SectionId}");
            }

            if (!string.Equals(section.Format, EffectSectionFormat, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"效果存档段格式不支持：{section.Format}");
            }

            if (!string.Equals(section.DataEncoding, SaveDataEncoding.Base64, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"效果存档段编码不支持：{section.DataEncoding}");
            }

            if (string.IsNullOrWhiteSpace(section.EncodedData))
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, "效果存档段数据为空。");
            }

            try
            {
                var readResult = TryReadEffectSaveData(section, out var saveData);
                if (!readResult.Succeeded)
                {
                    return readResult;
                }

                var importResult = effectController.ImportSnapshot(saveData);
                if (importResult == null || !importResult.Succeeded)
                {
                    return SaveSectionImportResult.Fail(
                        SaveSectionImportErrorCode.ImportFailed,
                        importResult != null ? importResult.Message : "效果控制器导入结果为空。");
                }

                return SaveSectionImportResult.Success();
            }
            catch (Exception ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.Unknown,
                    $"效果存档段导入异常：{ex.Message}");
            }
        }

        private static SaveSectionImportResult TryReadEffectSaveData(SaveSectionData section, out EffectSaveData saveData)
        {
            saveData = null;
            switch (section.SectionVersion)
            {
                case EffectSectionVersionV1:
                    return TryReadVersion1(section, out saveData);
                default:
                    return SaveSectionImportResult.Fail(
                        SaveSectionImportErrorCode.VersionUnsupported,
                        $"效果存档段版本不支持：{section.SectionVersion}");
            }
        }

        private static SaveSectionImportResult TryReadVersion1(SaveSectionData section, out EffectSaveData saveData)
        {
            saveData = null;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(section.EncodedData);
            }
            catch (FormatException ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"效果存档段 Base64 解码失败：{ex.Message}");
            }

            string json;
            try
            {
                json = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"效果存档段 UTF8 解码失败：{ex.Message}");
            }

            try
            {
                saveData = JsonUtility.FromJson<EffectSaveData>(json);
            }
            catch (ArgumentException ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"效果存档段 Json 解析失败：{ex.Message}");
            }

            return ValidateImportedSaveData(saveData);
        }

        [ContextMenu("NiumaEffectSave/注册到存档模块")]
        private void RegisterToSaveController()
        {
            if (_registeredToSaveController)
            {
                return;
            }

            ResolveReferences(true);
            if (saveController == null)
            {
                return;
            }

            var registered = saveController.RegisterProvider(this);
            _registeredToSaveController = registered;
            if (!registered)
            {
                Debug.LogWarning("[NiumaEffectSaveAdapter] 注册效果存档 Provider 失败。", this);
            }
        }

        [ContextMenu("NiumaEffectSave/从存档模块取消注册")]
        private void UnregisterFromSaveController()
        {
            ResolveReferences(false);
            if (_registeredToSaveController && saveController != null)
            {
                saveController.UnregisterProvider(SectionId);
            }

            _registeredToSaveController = false;
        }

        private void ResolveReferences(bool logMissing)
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (effectController == null)
            {
#if UNITY_2023_1_OR_NEWER
                effectController = FindFirstObjectByType<NiumaEffectController>();
#else
                effectController = FindObjectOfType<NiumaEffectController>();
#endif
            }

            if (saveController == null)
            {
#if UNITY_2023_1_OR_NEWER
                saveController = FindFirstObjectByType<NiumaSaveController>();
#else
                saveController = FindObjectOfType<NiumaSaveController>();
#endif
            }

            if (logMissing && effectController == null)
            {
                Debug.LogWarning("[NiumaEffectSaveAdapter] 未找到 NiumaEffectController，请在 Inspector 中绑定。", this);
            }

            if (logMissing && saveController == null)
            {
                Debug.LogWarning("[NiumaEffectSaveAdapter] 未找到 NiumaSaveController，请在 Inspector 中绑定。", this);
            }
        }

        private static void ValidateSaveDataForExport(EffectSaveData saveData)
        {
            var error = ValidateSaveData(saveData);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException($"效果存档导出数据无效：{error}");
            }
        }

        private static SaveSectionImportResult ValidateImportedSaveData(EffectSaveData saveData)
        {
            var error = ValidateSaveData(saveData);
            return string.IsNullOrWhiteSpace(error)
                ? SaveSectionImportResult.Success()
                : SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, $"效果存档段数据无效：{error}");
        }

        private static string ValidateSaveData(EffectSaveData saveData)
        {
            if (saveData == null)
            {
                return "解析结果为空。";
            }

            if (saveData.Version != 1)
            {
                return $"版本字段无效：{saveData.Version}";
            }

            if (saveData.Revision < 0L)
            {
                return $"Revision 不能为负数：{saveData.Revision}";
            }

            if (saveData.Owners == null)
            {
                return "Owners 字段为空引用。";
            }

            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            var effectInstanceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < saveData.Owners.Length; i++)
            {
                var owner = saveData.Owners[i];
                if (owner == null)
                {
                    return $"Owners[{i}] 为空。";
                }

                if (string.IsNullOrWhiteSpace(owner.ActorId))
                {
                    return $"Owners[{i}].ActorId 为空。";
                }

                if (!actorIds.Add(owner.ActorId))
                {
                    return $"重复 ActorId：{owner.ActorId}";
                }

                var error = ValidateOwner(owner, effectInstanceIds);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return $"ActorId={owner.ActorId}：{error}";
                }
            }

            return null;
        }

        private static string ValidateOwner(EffectOwnerSnapshot owner, HashSet<string> effectInstanceIds)
        {
            if (owner.Effects == null)
            {
                return "Effects 字段为空引用。";
            }

            for (var i = 0; i < owner.Effects.Length; i++)
            {
                var effect = owner.Effects[i];
                if (effect == null)
                {
                    return $"Effects[{i}] 为空。";
                }

                var error = ValidateEffect(effect, owner.ActorId, i, effectInstanceIds);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return error;
                }
            }

            return null;
        }

        private static string ValidateEffect(
            ActiveEffectSnapshot effect,
            string ownerActorId,
            int index,
            HashSet<string> effectInstanceIds)
        {
            if (string.IsNullOrWhiteSpace(effect.EffectInstanceId))
            {
                return $"Effects[{index}].EffectInstanceId 为空。";
            }

            if (!effectInstanceIds.Add(effect.EffectInstanceId))
            {
                return $"重复 EffectInstanceId：{effect.EffectInstanceId}";
            }

            if (string.IsNullOrWhiteSpace(effect.EffectId))
            {
                return $"EffectInstanceId={effect.EffectInstanceId} 的 EffectId 为空。";
            }

            if (string.IsNullOrWhiteSpace(effect.OwnerActorId))
            {
                return $"EffectInstanceId={effect.EffectInstanceId} 的 OwnerActorId 为空。";
            }

            if (!string.Equals(effect.OwnerActorId, ownerActorId, StringComparison.Ordinal))
            {
                return $"EffectInstanceId={effect.EffectInstanceId} 的 OwnerActorId 与 Owner.ActorId 不一致。";
            }

            if (effect.Type == EffectType.None)
            {
                return $"EffectInstanceId={effect.EffectInstanceId} 的 Type 不能为 None。";
            }

            if (effect.StackCount < 1)
            {
                return $"EffectInstanceId={effect.EffectInstanceId} 的 StackCount 必须大于 0。";
            }

            if (!IsFinite(effect.DurationSeconds) || effect.DurationSeconds < 0f)
            {
                return $"EffectInstanceId={effect.EffectInstanceId} 的 DurationSeconds 无效。";
            }

            if (!IsFinite(effect.RemainingSeconds) || effect.RemainingSeconds < 0f)
            {
                return $"EffectInstanceId={effect.EffectInstanceId} 的 RemainingSeconds 无效。";
            }

            var tagError = ValidateStringArray(effect.Tags, $"EffectInstanceId={effect.EffectInstanceId}.Tags");
            if (!string.IsNullOrWhiteSpace(tagError))
            {
                return tagError;
            }

            var customDataError = ValidateCustomData(effect.CustomData, effect.EffectInstanceId);
            if (!string.IsNullOrWhiteSpace(customDataError))
            {
                return customDataError;
            }

            return null;
        }

        private static string ValidateStringArray(string[] values, string fieldName)
        {
            if (values == null)
            {
                return $"{fieldName} 字段为空引用。";
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i]))
                {
                    return $"{fieldName}[{i}] 为空。";
                }

                if (!seen.Add(values[i]))
                {
                    return $"{fieldName} 存在重复值：{values[i]}";
                }
            }

            return null;
        }

        private static string ValidateCustomData(EffectCustomDataEntry[] customData, string effectInstanceId)
        {
            if (customData == null)
            {
                return $"EffectInstanceId={effectInstanceId}.CustomData 字段为空引用。";
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < customData.Length; i++)
            {
                var entry = customData[i];
                if (entry == null)
                {
                    return $"EffectInstanceId={effectInstanceId}.CustomData[{i}] 为空。";
                }

                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    return $"EffectInstanceId={effectInstanceId}.CustomData[{i}].Key 为空。";
                }

                if (!keys.Add(entry.Key))
                {
                    return $"EffectInstanceId={effectInstanceId}.CustomData 存在重复 Key：{entry.Key}";
                }
            }

            return null;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
