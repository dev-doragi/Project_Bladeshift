using UnityEngine;

public class PlayerRespawnPoint : MonoBehaviour
{
    [SerializeField] private bool _isDefaultPoint = false;

    public bool IsDefaultPoint => _isDefaultPoint;
    public Vector3 Position => transform.position;
}
