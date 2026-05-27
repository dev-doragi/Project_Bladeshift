using UnityEngine;

public sealed class BelialBossBattleTrigger : MonoBehaviour
{
    [SerializeField] private BelialBossCore _boss;
    [SerializeField] private GameObject _damageFloor;
    [SerializeField] private GameObject[] _exitBlockers;
    [SerializeField] private bool _disableTriggerAfterStart = true;

    private bool _started;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_started)
            return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            return;

        _started = true;

        _boss?.StartBattle();

        if (_damageFloor != null)
            _damageFloor.SetActive(true);

        if (_exitBlockers != null)
        {
            for (int i = 0; i < _exitBlockers.Length; i++)
            {
                if (_exitBlockers[i] != null)
                    _exitBlockers[i].SetActive(true);
            }
        }

        if (_disableTriggerAfterStart)
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c != null)
                c.enabled = false;
            else
                enabled = false;
        }
    }
}
