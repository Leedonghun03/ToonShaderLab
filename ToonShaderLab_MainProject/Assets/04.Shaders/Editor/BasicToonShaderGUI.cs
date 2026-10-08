using System;
using UnityEditor;
using UnityEngine;

namespace ToonShaderLab.Editor
{
    /// <summary>
    /// Basic Toon의 머티리얼 편집 화면. 렌더링 계산은 셰이더 파일에서 관리한다.
    /// 새 기능을 구현하면 해당 속성과 Draw... 영역을 여기에 추가한다.
    /// </summary>
    public sealed class BasicToonShaderGUI : ShaderGUI
    {
        private bool baseOpen = true;
        private bool shadingOpen = true;
        private bool previewOpen = true;
        private bool advancedOpen;
        private bool uvOpen;

        private static readonly GUIContent[] DisplayModes =
        {
            new GUIContent("최종 결과", "텍스처에 툰 명암과 주 광원의 색 / 세기를 적용합니다."),
            new GUIContent("텍스처만 보기", "텍스처와 전체 색상을 표시합니다. 셰이더의 조명과 안개 계산은 생략합니다."),
            new GUIContent("명암만 보기", "밝은 영역은 흰색, 어두운 영역은 검정색으로 표시합니다.")
        };
        private static readonly GUIContent[] VisibleFaces =
        {
            new GUIContent("양면 표시"),
            new GUIContent("뒷면만 표시"),
            new GUIContent("앞면만 표시 (기본)")
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // 선택이 바뀔 수 있으므로 MaterialProperty를 프레임마다 다시 찾는다.
            var baseMap = FindProperty("_BaseMap", properties);
            var baseColor = FindProperty("_BaseColor", properties);
            var litColor = FindProperty("_LitColor", properties);
            var shadowColor = FindProperty("_ShadowColor", properties);
            var threshold = FindProperty("_ShadeThreshold", properties);
            var softness = FindProperty("_ShadeSoftness", properties);
            var displayMode = FindProperty("_DisplayMode", properties);
            var cull = FindProperty("_Cull", properties);

            EditorGUILayout.LabelField("ToonShaderLab / 기본 툰 명암", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("텍스처와 빛 방향으로 기본 명암을 조절합니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6);

            DrawSection("1. 기본 색상", ref baseOpen, () => DrawBase(materialEditor, baseMap, baseColor));
            DrawSection("2. 툰 명암", ref shadingOpen, () =>
                DrawShading(materialEditor, properties, litColor, shadowColor, threshold, softness));
            DrawSection("3. 화면 확인", ref previewOpen, () => DrawPreview(materialEditor, displayMode));
            DrawSection("추가 설정", ref advancedOpen, () =>
                DrawPopup(materialEditor, cull, new GUIContent("표시할 면", "기본은 앞면만 표시합니다. 투명 표현을 켜는 기능은 아닙니다."), VisibleFaces));
        }

        private static void DrawSection(string title, ref bool isOpen, Action draw)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                isOpen = EditorGUILayout.Foldout(isOpen, title, true, EditorStyles.foldoutHeader);
                if (isOpen)
                {
                    EditorGUILayout.Space(3);
                    draw();
                    EditorGUILayout.Space(3);
                }
            }
            EditorGUILayout.Space(3);
        }

        private void DrawBase(MaterialEditor editor, MaterialProperty texture, MaterialProperty tint)
        {
            editor.TexturePropertySingleLine(new GUIContent("기본 텍스처", "모델의 UV에 맞춰 표시할 색상 텍스처입니다."), texture);
            DrawColor(editor, tint, new GUIContent("전체 색상", "텍스처 전체에 곱하는 색입니다. 흰색이면 원래 색상을 유지합니다."));
            if (!texture.hasMixedValue && texture.textureValue == null)
                EditorGUILayout.HelpBox("기본 텍스처를 넣어주세요. 비어 있으면 흰색 텍스처로 계산합니다.", MessageType.Info);

            uvOpen = EditorGUILayout.Foldout(uvOpen, "텍스처 반복 / 위치", true);
            if (uvOpen)
            {
                // Unity의 기본 편집기를 사용해 여러 머티리얼 편집과 Undo를 유지한다.
                editor.TextureScaleOffsetProperty(texture);
            }
        }

        private static void DrawShading(MaterialEditor editor, MaterialProperty[] properties,
            MaterialProperty lit, MaterialProperty shadow, MaterialProperty threshold, MaterialProperty softness)
        {
            DrawColor(editor, lit, new GUIContent("밝은 영역 색", "밝은 영역에 곱하는 색입니다. 흰색을 기준으로 조절하세요."));
            DrawColor(editor, shadow, new GUIContent("어두운 영역 색", "어두운 영역에 곱하는 색입니다. 검정에 가까울수록 어두워집니다."));
            editor.ShaderProperty(threshold, new GUIContent("명암 경계", "값을 높일수록 어두운 영역이 넓어집니다. 기본값은 0입니다."));
            editor.ShaderProperty(softness, new GUIContent("경계 부드러움", "값이 작으면 경계가 또렷하고, 크면 부드럽게 이어집니다. 기본값은 0.05입니다."));
            EditorGUILayout.LabelField("명암 경계 ↑ : 어두운 영역 증가 / 부드러움 ↑ : 완만한 경계", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);
            if (GUILayout.Button(new GUIContent("명암 설정 초기화", "선택한 머티리얼의 명암 색 / 경계 / 부드러움을 셰이더 기본값으로 되돌립니다. 텍스처와 전체 색상은 유지합니다. Ctrl+Z로 취소할 수 있습니다.")))
                ResetShading(editor, properties);
        }

        private static void DrawPreview(MaterialEditor editor, MaterialProperty mode)
        {
            DrawPopup(editor, mode, new GUIContent("표시 모드", "확인하려는 내용을 선택합니다. 머티리얼에 저장되며 Game 화면에도 적용됩니다."), DisplayModes);
            if (mode.hasMixedValue)
            {
                EditorGUILayout.HelpBox("서로 다른 표시 모드가 선택되어 있습니다. 모드를 선택하면 함께 변경됩니다.", MessageType.Info);
                return;
            }

            int selected = Mathf.Clamp(Mathf.RoundToInt(mode.floatValue), 0, 2);
            string message = selected == 0
                ? "주 방향광의 회전을 바꿔 명암을 확인하세요. 최종 결과는 광원의 색과 세기도 반영합니다."
                : selected == 1
                    ? "텍스처와 전체 색상만 표시합니다. 카메라 후처리는 별도로 적용될 수 있습니다."
                    : "흰색은 밝은 영역, 검정색은 어두운 영역입니다. 확인이 끝나면 최종 결과로 돌아오세요.";
            EditorGUILayout.HelpBox(message, MessageType.None);
        }

        private static void DrawColor(MaterialEditor editor, MaterialProperty property, GUIContent label)
        {
            // 이 셰이더는 RGB만 사용하므로 효과가 없는 알파 항목은 숨긴다.
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            var color = EditorGUILayout.ColorField(label, property.colorValue, true, false, false);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = previousMixed;
            if (changed)
            {
                editor.RegisterPropertyChangeUndo(label.text);
                property.colorValue = color;
            }
        }

        private static void DrawPopup(MaterialEditor editor, MaterialProperty property, GUIContent label, GUIContent[] options)
        {
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(label, Mathf.Clamp(Mathf.RoundToInt(property.floatValue), 0, options.Length - 1), options);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = previousMixed;
            if (changed)
            {
                editor.RegisterPropertyChangeUndo(label.text);
                property.floatValue = selected;
            }
        }

        private static void ResetShading(MaterialEditor editor, MaterialProperty[] properties)
        {
            // 4개 속성을 한 번의 Undo로 되돌릴 수 있도록 묶는다.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("툰 명암 설정 초기화");
            editor.RegisterPropertyChangeUndo("툰 명암 설정 초기화");
            FindProperty("_LitColor", properties).colorValue = Color.white;
            FindProperty("_ShadowColor", properties).colorValue = new Color(0.78f, 0.70f, 0.76f, 1);
            FindProperty("_ShadeThreshold", properties).floatValue = 0;
            FindProperty("_ShadeSoftness", properties).floatValue = 0.05f;
            Undo.CollapseUndoOperations(group);
        }
    }
}
