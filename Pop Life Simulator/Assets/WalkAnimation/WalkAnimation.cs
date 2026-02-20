using UnityEngine;

public class WalkAnimation : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite body;
    [SerializeField] private Sprite leftFeet;
    [SerializeField] private Sprite rightFeet;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer leftFeetRenderer;
    [SerializeField] private SpriteRenderer rightFeetRenderer;

    [Header("Walk Settings")]
    [SerializeField] private float moveRange = 0.1f;
    [SerializeField] private float moveSpeed = 5f;

    [Header("Debug")]
    [SerializeField] private bool testMode;

    private bool isWalking;

    private Vector3 leftFeetOriginPos;
    private Vector3 rightFeetOriginPos;

    private void Awake()
    {
        // Assign sprites
        bodyRenderer.sprite = body;
        leftFeetRenderer.sprite = leftFeet;
        rightFeetRenderer.sprite = rightFeet;

        // Cache original positions
        leftFeetOriginPos = leftFeetRenderer.transform.localPosition;
        rightFeetOriginPos = rightFeetRenderer.transform.localPosition;
    }

    private void Update()
    {
        if (testMode)
        {
            isWalking = Input.GetKey(KeyCode.T);
        }

        if (isWalking)
        {
            OnWalk();
        }
        else
        {
            OnStand();
        }
    }

    private void OnWalk()
    {
        float sin = Mathf.Sin(Time.time * moveSpeed);

        // 右脚：只取正半波
        float rightOffset = Mathf.Max(0f, sin) * moveRange;

        // 左脚：相位差 π
        float leftOffset = Mathf.Max(0f, -sin) * moveRange;

        rightFeetRenderer.transform.localPosition =
            rightFeetOriginPos + Vector3.up * rightOffset;
        
        leftFeetRenderer.transform.localPosition =
            leftFeetOriginPos + Vector3.up * leftOffset;
    }


    private void OnStand()
    {
        // 平滑回到原位
        leftFeetRenderer.transform.localPosition =
            Vector3.Lerp(leftFeetRenderer.transform.localPosition,
                         leftFeetOriginPos,
                         Time.deltaTime * moveSpeed);

        rightFeetRenderer.transform.localPosition =
            Vector3.Lerp(rightFeetRenderer.transform.localPosition,
                         rightFeetOriginPos,
                         Time.deltaTime * moveSpeed);
    }

    public void StartWalk()
    {
        isWalking = true;
    }

    public void StopWalk()
    {
        isWalking = false;
    }
}
