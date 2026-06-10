using System;
using System.Collections;
using UnityEngine;

public class HitParticle : MonoBehaviour
{
    private ParticleSystem ps;
    private Action<GameObject> onComplete; // 반납용 콜백

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }

    public void Play(Vector3 worldPos, Action<GameObject> returnAction)
    {
        onComplete = returnAction;

        transform.position = worldPos;
        ps.Play();

        StopAllCoroutines();
        StartCoroutine(WaitAndReturn());
    }

    private IEnumerator WaitAndReturn()
    {
        // 파티클의 재생 시간만큼 딱 대기
        yield return new WaitForSeconds(ps.main.duration);

        onComplete?.Invoke(gameObject);
    }
}
