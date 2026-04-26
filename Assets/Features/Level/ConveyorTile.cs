using UnityEngine;

public class ConveyorTile : MonoBehaviour
{
    private enum ConveyorDirection
    {
        North,
        East,
        South,
        West
    }

    [Header("Sound")]
    [SerializeField] private AudioSource conveyorSound;

    private ConveyorDirection direction;

    private void Awake()
    {
        direction = YawToDirection(transform.eulerAngles.y);
    }

    public Vector3Int DirectionVector => direction switch
    {
        ConveyorDirection.North => new Vector3Int(0, 0, 1),
        ConveyorDirection.East => new Vector3Int(1, 0, 0),
        ConveyorDirection.South => new Vector3Int(0, 0, -1),
        ConveyorDirection.West => new Vector3Int(-1, 0, 0),
        _ => new Vector3Int(0, 0, 1)
    };

    private void OnEnable()
    {
        GridHandler.Instance?.RefreshConveyorsFromScene();
    }

    private void OnDisable()
    {
        SetOccupiedState(false);
        GridHandler.Instance?.RefreshConveyorsFromScene();
    }

    public void SetOccupiedState(bool isOccupied)
    {
        if (conveyorSound == null)
        {
            return;
        }

        if (isOccupied)
        {
            if (!conveyorSound.isPlaying)
            {
                conveyorSound.Play();
            }
        }
        else if (conveyorSound.isPlaying)
        {
            conveyorSound.Stop();
        }
    }

    public void ResetState()
    {
        direction = YawToDirection(transform.eulerAngles.y);
        SetOccupiedState(false);
    }

    private static ConveyorDirection YawToDirection(float yaw)
    {
        int snapped = Mathf.RoundToInt(yaw / 90f) * 90;
        int normalized = ((snapped % 360) + 360) % 360;

        return normalized switch
        {
            0 => ConveyorDirection.East,
            90 => ConveyorDirection.South,
            180 => ConveyorDirection.West,
            270 => ConveyorDirection.North,
            _ => ConveyorDirection.East
        };
    }
}
