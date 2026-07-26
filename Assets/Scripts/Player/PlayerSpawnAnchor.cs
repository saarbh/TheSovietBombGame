using UnityEngine;

/// <summary>
/// Marks where the persistent player should be placed when this scene loads.
/// Drop one empty GameObject carrying this component into every scene the player
/// travels to - <see cref="PersistentPlayerSpawner"/> looks it up after each load.
///
/// The transform's position and Y rotation are used; pitch and roll are ignored so a
/// carelessly tilted anchor can't leave the player looking at the floor.
/// </summary>
public class PlayerSpawnAnchor : MonoBehaviour
{
    [Tooltip("Optional id. Leave empty for the scene's default spawn. Use ids when a scene " +
             "has several entry points and the caller picks one by name.")]
    [SerializeField] private string spawnId = string.Empty;

    /// <summary>Identifier for this anchor, or empty when it is the scene default.</summary>
    public string SpawnId => spawnId;

    /// <summary>World position the player is placed at.</summary>
    public Vector3 SpawnPosition => transform.position;

    /// <summary>Yaw-only rotation, so the player always stands upright.</summary>
    public Quaternion SpawnRotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

    private void OnDrawGizmos()
    {
        // Drawn always (not just when selected) so the spawn is visible while dressing a scene.
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + (Vector3.up * 0.9f), 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3.up * 1.8f));

        // Facing arrow - which way the player will look on arrival.
        var forward = SpawnRotation * Vector3.forward;
        Gizmos.DrawLine(transform.position + (Vector3.up * 0.9f),
                        transform.position + (Vector3.up * 0.9f) + (forward * 1f));
    }
}
