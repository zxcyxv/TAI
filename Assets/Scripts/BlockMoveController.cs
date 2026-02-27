using UnityEngine;

public class BlockMoveController : MonoBehaviour
{
    // Singleton
    private static BlockMoveController instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static BlockMoveController Instance
    {
        get { return instance; }
    }

    private float elapsedTime = 0f;


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveY();
        if (Input.GetKeyDown(KeyCode.RightArrow)) MoveRight();
        if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveLeft();

        elapsedTime += Time.deltaTime;
        if (elapsedTime >= 0.5f)
        {
            MoveY();
        }
    }

    void MoveY()
    {
        elapsedTime = 0f;
    }

    void MoveLeft()
    {
        
    }

    void MoveRight()
    {
        
    }
}
