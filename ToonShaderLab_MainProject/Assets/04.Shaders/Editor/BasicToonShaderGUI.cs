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
        private bool outlineOpen = true;
        private bool highlightOpen = true;
        private bool emissionOpen = true;
        private bool previewOpen = true;
        private bool advancedOpen;
        private bool uvOpen;

        private static readonly GUIContent[] DisplayModes =
        {
            new GUIContent("최종 결과", "텍스처에 툰 명암과 주 광원의 색 / 세기를 적용하고, 선택한 부위에 하이라이트를 더합니다."),
            new GUIContent("텍스처만 보기", "텍스처와 전체 색상을 표시합니다. 셰이더의 조명과 안개 계산은 생략합니다."),
            new GUIContent("명암만 보기", "밝은 영역은 흰색, 어두운 영역은 검정색으로 표시합니다."),
            new GUIContent("하이라이트 범위", "범위 마스크의 밝은 영역이 하이라이트를 적용할 부위입니다."),
            new GUIContent("금속성 (M.G)", "재질 마스크의 G 채널을 표시합니다."),
            new GUIContent("반사 마스크 (M.B)", "재질 마스크의 B 채널을 표시합니다."),
            new GUIContent("매끄러움 (A.G)", "표면 마스크의 G 채널을 표시합니다."),
            new GUIContent("하이라이트만 보기", "기본색과 툰 명암을 제외한 하이라이트 기여분을 표시합니다."),
            new GUIContent("발광 범위", "원본 UV0의 발광 마스크 R 채널을 표시합니다."),
            new GUIContent("발광만 보기", "주 광원과 툰 명암에 독립적인 표면 발광만 표시합니다.")
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
            var shadowStrength = FindProperty("_ShadowStrength", properties);
            var threshold = FindProperty("_ShadeThreshold", properties);
            var softness = FindProperty("_ShadeSoftness", properties);
            var outlineEnabled = FindProperty("_OutlineEnabled", properties);
            var outlineWidth = FindProperty("_OutlineWidth", properties);
            var outlineColor = FindProperty("_OutlineColor", properties);
            var highlightEnabled = FindProperty("_HighlightEnabled", properties);
            var highlightMask = FindProperty("_HighlightMask", properties);
            var materialMask = FindProperty("_MaterialMask", properties);
            var surfaceMask = FindProperty("_SurfaceMask", properties);
            var highlightColor = FindProperty("_HighlightColor", properties);
            var highlightIntensity = FindProperty("_HighlightIntensity", properties);
            var highlightSmoothness = FindProperty("_HighlightSmoothness", properties);
            var displayMode = FindProperty("_DisplayMode", properties);
            var cull = FindProperty("_Cull", properties);

            EditorGUILayout.LabelField("ToonShaderLab / Basic Toon", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("부위별 색상 / 명암 / 외곽선 / 하이라이트를 조절합니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6);

            DrawSection("1. 기본 색상", ref baseOpen, () =>
            {
                DrawBase(materialEditor, baseMap, baseColor);
                var regionColor = FindProperty("_RegionColorEnabled", properties);
                materialEditor.ShaderProperty(regionColor, new GUIContent("부위별 기본색 보정", "R/G 마스크로 지정한 부위의 색만 조정합니다. 조명과 툰 명암 경계는 유지됩니다."));
                if (regionColor.hasMixedValue || regionColor.floatValue > 0.5f)
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("색상 부위 마스크", "유즈하: R 피부 / G 머리카락. 검정 영역은 보정하지 않습니다."), FindProperty("_ColorRegionMask", properties));
                    materialEditor.ShaderProperty(FindProperty("_RegionTintR", properties), "R 영역 색 배율");
                    materialEditor.ShaderProperty(FindProperty("_RegionSaturationR", properties), "R 영역 채도");
                    materialEditor.ShaderProperty(FindProperty("_RegionTintG", properties), "G 영역 색 배율");
                    materialEditor.ShaderProperty(FindProperty("_RegionSaturationG", properties), "G 영역 채도");
                }
                var localTone = FindProperty("_LocalToneEnabled", properties);
                materialEditor.ShaderProperty(localTone, new GUIContent("볼·머리 음영 보정", "기존 툰 명암 경계를 유지하며 전용 마스크로 색과 음영 깊이를 조정합니다."));
                if (localTone.hasMixedValue || localTone.floatValue > 0.5f)
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("국소 보정 마스크", "R: 볼 / G: 텍스처의 머리 음영 / B: 머리 전체. 눈과 의상은 검정으로 제외합니다."), FindProperty("_LocalToneMask", properties));
                    DrawColor(materialEditor, FindProperty("_CheekTint", properties), new GUIContent("볼 색 배율"));
                    materialEditor.ShaderProperty(FindProperty("_CheekStrength", properties), "볼 붉은 기운");
                    materialEditor.ShaderProperty(FindProperty("_HairDepthTint", properties), "머리 음영 색 배율");
                    materialEditor.ShaderProperty(FindProperty("_HairDepthStrength", properties), "머리 음영 깊이");
                }
                var darkDetail = FindProperty("_DarkDetailEnabled", properties);
                materialEditor.ShaderProperty(darkDetail, new GUIContent("검정 디테일 색 보정", "현재 머티리얼의 검정에 가까운 텍스처 영역만 색을 바꿉니다. 알파나 그리는 순서는 변경하지 않습니다."));
                if (darkDetail.hasMixedValue || darkDetail.floatValue > 0.5f)
                    DrawColor(materialEditor, FindProperty("_DarkDetailColor", properties), new GUIContent("검정 디테일 대체 색", "조명 적용 전의 색입니다. 속눈썹처럼 순수 검정으로 뭉치는 디테일에 사용합니다."));
            });
            DrawSection("2. 툰 명암", ref shadingOpen, () =>
                DrawShading(materialEditor, properties, litColor, shadowColor, shadowStrength, threshold, softness));
            DrawSection("3. 아웃라인", ref outlineOpen, () =>
                DrawOutline(materialEditor, outlineEnabled, outlineWidth, outlineColor));
            DrawSection("4. 기본 하이라이트", ref highlightOpen, () =>
                DrawHighlight(materialEditor, highlightEnabled, highlightMask, materialMask, surfaceMask,
                    highlightColor, highlightIntensity, highlightSmoothness));
            DrawSection("5. 기본 발광", ref emissionOpen, () => DrawEmission(materialEditor, properties));
            DrawSection("6. 화면 확인", ref previewOpen, () => DrawPreview(materialEditor, displayMode));
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
            MaterialProperty lit, MaterialProperty shadow, MaterialProperty strength,
            MaterialProperty threshold, MaterialProperty softness)
        {
            DrawColor(editor, lit, new GUIContent("밝은 영역 색", "밝은 영역에 곱하는 색입니다. 흰색을 기준으로 조절하세요."));
            DrawColor(editor, shadow, new GUIContent("그림자 색", "어두운 영역에 곱하는 색입니다. 그림자 강도에 따라 밝은 영역 색과 섞입니다."));
            editor.ShaderProperty(strength, new GUIContent("그림자 강도", "0: 밝은 영역 색만 사용 / 1: 그림자 색을 모두 반영. 명암 경계 위치와 광원의 색 / 세기는 유지합니다."));
            EditorGUILayout.LabelField("부위별 머티리얼을 선택하면 색과 강도를 각각 조절할 수 있습니다.", EditorStyles.wordWrappedMiniLabel);
            editor.ShaderProperty(threshold, new GUIContent("명암 경계", "값을 높일수록 어두운 영역이 넓어집니다. 기본값은 0입니다."));
            editor.ShaderProperty(softness, new GUIContent("경계 부드러움", "값이 작으면 경계가 또렷하고, 크면 부드럽게 이어집니다. 기본값은 0.05입니다."));
            EditorGUILayout.LabelField("명암 경계 ↑ : 어두운 영역 증가 / 부드러움 ↑ : 완만한 경계", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);
            if (GUILayout.Button(new GUIContent("명암 설정 초기화", "선택한 머티리얼의 명암 색 / 강도 / 경계 / 부드러움을 셰이더 공통 기본값으로 되돌립니다. 강도는 1이며 얼굴용 시작값도 초기화됩니다. 텍스처와 전체 색상은 유지합니다. Ctrl+Z로 취소할 수 있습니다.")))
                ResetShading(editor, properties);
        }

        private static void DrawOutline(MaterialEditor editor, MaterialProperty enabled,
            MaterialProperty width, MaterialProperty color)
        {
            editor.ShaderProperty(enabled, new GUIContent("아웃라인 사용", "표면 방향으로 확장한 메시의 뒷면을 그립니다. 부위별로 켜고 끌 수 있습니다."));
            using (new EditorGUI.DisabledScope(!enabled.hasMixedValue && enabled.floatValue < 0.5f))
            {
                editor.ShaderProperty(width, new GUIContent("두께 (픽셀)", "렌더링 해상도 기준 두께입니다. 0이면 선이 사라집니다. 렌더 스케일 / 후처리에 따라 최종 화면의 두께는 달라질 수 있습니다."));
                DrawColor(editor, color, new GUIContent("선 색상", "이 부위의 외곽선 색상입니다. 머리카락과 피부는 서로 다른 색상으로 조절할 수 있습니다."));
            }
            EditorGUILayout.LabelField("아웃라인은 최종 결과 모드에서만 표시합니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Scene / Game 화면에서 확인하세요. 머티리얼 미리보기에는 아웃라인이 표시되지 않습니다.", EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawHighlight(MaterialEditor editor, MaterialProperty enabled,
            MaterialProperty regionMask, MaterialProperty materialMask, MaterialProperty surfaceMask,
            MaterialProperty color, MaterialProperty intensity, MaterialProperty smoothness)
        {
            editor.ShaderProperty(enabled, new GUIContent("하이라이트 사용", "기존 툰 명암을 유지하면서 주 광원과 시선 방향에 따른 기본 스페큘러를 선택한 부위에 더합니다."));
            editor.TexturePropertySingleLine(new GUIContent("적용 범위 마스크", "흰색: 적용 / 검정: 제외. R 채널을 사용합니다. 비어 있으면 하이라이트가 적용되지 않습니다."), regionMask);
            editor.TexturePropertySingleLine(new GUIContent("재질 마스크 (M)", "G: 금속성 / B: 반사 마스크. R의 재질 ID만으로 적용 부위를 구분하지 않습니다."), materialMask);
            editor.TexturePropertySingleLine(new GUIContent("표면 마스크 (A)", "G: 매끄러움. B 채널을 자동으로 발광 처리하지 않습니다."), surfaceMask);
            EditorGUILayout.LabelField("마스크는 모델의 원래 UV0를 사용합니다. 기본 텍스처의 반복 / 위치 설정과 별개입니다.", EditorStyles.wordWrappedMiniLabel);

            if (!regionMask.hasMixedValue && regionMask.textureValue == null)
                EditorGUILayout.HelpBox("적용 범위 마스크를 넣으면 밝은 영역에만 하이라이트가 적용됩니다. 먼저 ‘하이라이트 범위’ 모드로 선택 부위를 확인하세요.", MessageType.Info);
            if ((enabled.hasMixedValue || enabled.floatValue >= 0.5f) &&
                ((!materialMask.hasMixedValue && materialMask.textureValue == null) ||
                 (!surfaceMask.hasMixedValue && surfaceMask.textureValue == null)))
                EditorGUILayout.HelpBox("재질 마스크 (M)와 표면 마스크 (A)를 연결해 부위별 금속성 / 반사 / 매끄러움을 반영하세요.", MessageType.Warning);

            WarnIfSrgbTexture(regionMask, "적용 범위 마스크");
            WarnIfSrgbTexture(materialMask, "재질 마스크 (M)");
            WarnIfSrgbTexture(surfaceMask, "표면 마스크 (A)");

            using (new EditorGUI.DisabledScope(!enabled.hasMixedValue && enabled.floatValue < 0.5f))
            {
                DrawColor(editor, color, new GUIContent("하이라이트 색", "주 광원의 색에 곱할 반사색입니다. 흰색을 기준으로 조절하세요."));
                editor.ShaderProperty(intensity, new GUIContent("하이라이트 강도", "선택한 영역의 추가 반사 밝기입니다. 0이면 기존 툰 명암만 표시합니다."));
                editor.ShaderProperty(smoothness, new GUIContent("매끄러움 배율", "표면 마스크 A.G에 곱합니다. 값이 커질수록 반사가 좁고 선명해집니다."));
            }
            EditorGUILayout.LabelField("카메라와 조명을 고정한 상태에서 사용 여부를 바꿔 적용 전후를 비교하세요.", EditorStyles.wordWrappedMiniLabel);
        }

        private static void WarnIfSrgbTexture(MaterialProperty property, string label)
        {
            if (property.hasMixedValue || property.textureValue == null)
                return;

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(property.textureValue)) as TextureImporter;
            if (importer != null && importer.sRGBTexture)
                EditorGUILayout.HelpBox(label + "는 데이터 텍스처입니다. Import Settings에서 sRGB (Color Texture)를 꺼야 원래 채널값으로 계산됩니다.", MessageType.Warning);
        }

        private void DrawEmission(MaterialEditor editor, MaterialProperty[] properties)
        {
            var enabled = FindProperty("_EmissionEnabled", properties);
            var mask = FindProperty("_EmissionMask", properties);
            editor.ShaderProperty(enabled, new GUIContent("발광 사용", "선택 부위에 조명과 독립적인 표면 발광을 더합니다."));
            editor.TexturePropertySingleLine(new GUIContent("발광 범위 마스크", "R 채널. 원본 UV0를 사용하며 기본색 타일링과 독립적입니다."), mask);
            WarnIfSrgbTexture(mask, "발광 범위 마스크");
            if (!mask.hasMixedValue && mask.textureValue == null)
                EditorGUILayout.HelpBox("발광 범위 마스크가 없으면 발광하지 않습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!enabled.hasMixedValue && enabled.floatValue < 0.5f))
            {
                DrawColor(editor, FindProperty("_SurfaceEmissionColor", properties), new GUIContent("발광 색 배율", "기본 텍스처의 원래 색에 곱합니다. 흰색은 원래 색을 유지합니다."));
                editor.ShaderProperty(FindProperty("_EmissionIntensity", properties), new GUIContent("발광 강도", "0이면 추가 발광이 없습니다. HDR 밝기는 Bloom 적용 시 번짐에 영향을 줍니다."));
            }
            EditorGUILayout.LabelField("표면 발광과 Bloom은 별개입니다. 먼저 후처리를 끄고 적용 범위를 확인하세요.", EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawPreview(MaterialEditor editor, MaterialProperty mode)
        {
            DrawPopup(editor, mode, new GUIContent("표시 모드", "확인하려는 내용을 선택합니다. 머티리얼에 저장되며 Game 화면에도 적용됩니다."), DisplayModes);
            if (mode.hasMixedValue)
            {
                EditorGUILayout.HelpBox("서로 다른 표시 모드가 선택되어 있습니다. 모드를 선택하면 함께 변경됩니다.", MessageType.Info);
                return;
            }

            int selected = Mathf.Clamp(Mathf.RoundToInt(mode.floatValue), 0, DisplayModes.Length - 1);
            string message;
            switch (selected)
            {
                case 1:
                    message = "텍스처와 전체 색상만 표시합니다. 카메라 후처리는 별도로 적용될 수 있습니다.";
                    break;
                case 2:
                    message = "색과 강도를 적용하기 전의 영역입니다. 흰색은 밝은 영역, 검정색은 어두운 영역입니다. 그림자 강도는 최종 결과에서 확인하세요.";
                    break;
                case 3:
                    message = "밝은 부분이 선택된 범위입니다. 의도한 금속 부위만 선택되고 주변 가죽 / 머리카락 / 의상에 번지지 않는지 확인하세요. 실제 반사에는 재질 마스크와 광원 / 시선 방향도 반영됩니다.";
                    break;
                case 4:
                    message = "재질 마스크 M.G의 금속성입니다. 같은 값이 다른 장식에도 있으므로 적용 범위는 별도 마스크로 제한합니다.";
                    break;
                case 5:
                    message = "재질 마스크 M.B의 반사 마스크입니다. 검은 홈과 무늬는 반사를 억제합니다.";
                    break;
                case 6:
                    message = "표면 마스크 A.G의 매끄러움입니다. 매끄러움 배율을 적용하기 전의 데이터입니다.";
                    break;
                case 8:
                    message = "발광 마스크의 선택 영역입니다. 같은 재질을 공유하는 다른 부위에 번지지 않는지 확인하세요.";
                    break;
                case 9:
                    message = "조명과 독립적인 추가 발광입니다. 발광이 꺼져 있거나 강도가 0이면 검정색입니다.";
                    break;
                case 7:
                    message = "추가되는 하이라이트만 표시합니다. 광원 또는 카메라를 움직여 반응을 확인하세요. 하이라이트가 꺼져 있거나 강도가 0이면 검정색입니다.";
                    break;
                default:
                    message = "주 방향광의 회전을 바꿔 명암을 확인하세요. 최종 결과는 광원의 색과 세기도 반영합니다.";
                    break;
            }
            if (selected >= 3)
                message += " 채널값 비교 시 카메라 후처리를 끄세요.";
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
            // 명암 속성을 한 번의 Undo로 되돌릴 수 있도록 묶는다.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("툰 명암 설정 초기화");
            editor.RegisterPropertyChangeUndo("툰 명암 설정 초기화");
            FindProperty("_LitColor", properties).colorValue = Color.white;
            FindProperty("_ShadowColor", properties).colorValue = new Color(0.78f, 0.70f, 0.76f, 1);
            FindProperty("_ShadowStrength", properties).floatValue = 1;
            FindProperty("_ShadeThreshold", properties).floatValue = 0;
            FindProperty("_ShadeSoftness", properties).floatValue = 0.05f;
            Undo.CollapseUndoOperations(group);
        }
    }
}
