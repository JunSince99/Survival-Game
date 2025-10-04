using UnityEngine;
using UnityEngine.Animations.Rigging;

public class HeadController : MonoBehaviour
{
    [Header("Refs")]
    public Transform characterRoot;     // 캐릭터 전방 기준(루트)
    public Transform head;              // 머리 본(기준점)
    public Camera cam;                  // 카메라
    public MultiAimConstraint headAim;  // 머리 Multi-Aim
    public Transform lookTargetProxy;   // 타깃 프록시(빈 오브젝트)

    [Header("Orbit Settings (원형 궤도)")]
    public float followDistance = 5f;        // 원 반지름
    public float heightOffset = 0.0f;        // 높이 고정 오프셋
    public float backAngleLimit = 165f;      // ±이 값 이상으로는 못 감(도 단위, 0~180)

    [Header("Angle Smoothing")]
    public float angularSmoothTime = 0.25f;  // 각도 보간 시간(작을수록 즉각적)
    public float weightSmoothTime = 0.10f;   // (선택) 가중치 보간

    // 내부 상태
    float _currentAngleDeg = 0f;  // 현재 타깃의 방위각(도)
    float _angleVel = 0f;         // SmoothDamp용 속도
    float _weightVel = 0f;        // 가중치 보간용

    void LateUpdate()
    {
        if (!cam || !characterRoot || !lookTargetProxy || !headAim) return;

        // 기준 위치(머리 또는 루트)
        Vector3 headBase = head ? head.position : characterRoot.position;

        // 수평면 기준 전방/카메라 방향
        Vector3 fwd = characterRoot.forward; fwd.y = 0; if (fwd.sqrMagnitude < 1e-6f) return; fwd.Normalize();
        Vector3 camDir = cam.transform.forward; camDir.y = 0; if (camDir.sqrMagnitude < 1e-6f) return; camDir.Normalize();

        // 원하는 '목표 각도'(SignedAngle, -180~+180)를 계산하고 ±backAngleLimit로 클램프
        float desiredAngleDeg = Vector3.SignedAngle(fwd, camDir, Vector3.up);
        desiredAngleDeg = Mathf.Clamp(desiredAngleDeg, -backAngleLimit, backAngleLimit);

        // ★ 포인트: 위치가 아닌 '각도'를 보간 → 원형 궤도를 따라 이동
        _currentAngleDeg = Mathf.SmoothDamp(
            _currentAngleDeg, desiredAngleDeg, ref _angleVel, angularSmoothTime
        );

        // 원 위의 방향 벡터 = 캐릭터 정면(fwd)을 _currentAngleDeg만큼 Y축으로 회전
        Vector3 orbitDir = Quaternion.AngleAxis(_currentAngleDeg, Vector3.up) * fwd;

        // 원 위 목표 위치(반지름 고정), Y는 캐릭터 기준 높이로 '고정'
        Vector3 orbitPos = headBase + orbitDir * followDistance;
        orbitPos.y = headBase.y + heightOffset;

        // 프록시를 즉시 위치시킴(이미 각도에서 스무딩 했으므로 위치 보간 불필요)
        lookTargetProxy.position = orbitPos;

        // (선택) 가중치: 뒤쪽 제한에 걸렸을 땐 0, 아니면 1로… 필요 없으면 항상 1로 두셔도 됩니다.
        float targetW = (Mathf.Abs(desiredAngleDeg) >= backAngleLimit - 0.001f) ? 0f : 1f;
        headAim.weight = Mathf.SmoothDamp(headAim.weight, targetW, ref _weightVel, weightSmoothTime);
        // 항상 따라가게 하려면: headAim.weight = 1f;
    }
}
