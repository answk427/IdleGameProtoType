using UnityEngine;

// StageData가 이제 [SerializeField] 필드 기반이라 Unity 직렬화가 되므로,
// MonsterEntry와 동일한 패턴으로 필드를 복제하지 않고 그대로 참조한다.
// bgTexturePath(문자열) 대신 실제 텍스처 레퍼런스를 들고 있는 게 핵심 차이.
[System.Serializable]
public class StageEntry
{
    public StageData data;
    public Texture2D backgroundTexture;
}

