using UnityEditor;
using UnityEditor.UI;

// Button의 기본 인스펙터는 상속한 클래스의 필드를 그리지 않는다. UIButton의 소리 종류를 보이게 한다.
[CustomEditor(typeof(UIButton), true)]
[CanEditMultipleObjects]
public class UIButtonEditor : ButtonEditor
{
    private SerializedProperty sound;

    protected override void OnEnable()
    {
        base.OnEnable();
        sound = serializedObject.FindProperty("sound");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();
        EditorGUILayout.PropertyField(sound);
        serializedObject.ApplyModifiedProperties();
    }
}
