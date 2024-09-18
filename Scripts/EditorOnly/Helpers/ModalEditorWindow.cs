using System;
using UnityEngine;
using UnityEditor;

public class ModalEditorWindow : EditorWindow
{
    public readonly struct ModalEditorWindowSettings
    {
        public string Title { get; init; }
        public string Message { get; init; }
        public string ConfirmText { get; init; }
        public string CancelText { get; init; }
        public Action OnConfirm { get; init; }
        public Action OnCancel { get; init; }
        public Action OnGuiLayoutContent { get; init; }
    }

    private ModalEditorWindowSettings _settings;
    
    public static void ShowModalWindow(in ModalEditorWindowSettings settings)
    {
        var window = CreateInstance<ModalEditorWindow>();
        window.titleContent = new GUIContent(settings.Title);
        window.minSize = new Vector2(400, 250);
        window.maxSize = new Vector2(400, 250);
        
        window._settings = settings;
        
        window.CenterWindow();
        window.ShowModalUtility();
        window.CenterWindow();
    }

    private void CenterWindow()
    {
        var main = EditorGUIUtility.GetMainWindowPosition();
        var pos = position;

        var centerX = main.x + (main.width - pos.width) * 0.5f;
        var centerY = main.y + (main.height - pos.height) * 0.5f;

        position = new Rect(centerX, centerY, pos.width, pos.height);
    }

    private void OnGUI()
    {
        GUILayout.Label(_settings.Message, EditorStyles.wordWrappedLabel);

        GUILayout.Space(20);
        
        _settings.OnGuiLayoutContent?.Invoke();
        
        GUILayout.BeginHorizontal();

        if (_settings.OnConfirm != null)
        {
            if (GUILayout.Button(_settings.ConfirmText ?? "OK"))
            {
                _settings.OnConfirm?.Invoke();
                Close();
            }
        }
        
        if (_settings.OnCancel != null)
        {
            if (GUILayout.Button(_settings.CancelText ?? "Cancel"))
            {
                _settings.OnCancel?.Invoke();
                Close();
            }
        }

        GUILayout.EndHorizontal();
    }
}