using UnityEngine;

[DisallowMultipleComponent]
public class MotorAbilityPolicy2D : MonoBehaviour
{
    private int _enemyLayer = -1;
    private int _dashCollisionPlayerLayer = -1;
    private bool _isIgnoringEnemyCollision;
    private int _dashInvincibleLayer = -1;
    private int _dashOriginalLayer = -1;
    private bool _isDashLayerOverridden;

    public void Configure(string enemyLayerName, string invincibleLayerName)
    {
        _enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        _dashInvincibleLayer = LayerMask.NameToLayer(invincibleLayerName);
    }

    public void ApplyDashStart(GameObject owner, bool ignoreEnemyCollisionWhileDashing, bool useInvincibleLayerWhileDashing)
    {
        SetEnemyCollisionIgnoredForDash(owner, ignoreEnemyCollisionWhileDashing, true);
        SetDashInvincibleLayer(owner, useInvincibleLayerWhileDashing, true);
    }

    public void ApplyDashEnd(GameObject owner, bool ignoreEnemyCollisionWhileDashing, bool useInvincibleLayerWhileDashing)
    {
        SetEnemyCollisionIgnoredForDash(owner, ignoreEnemyCollisionWhileDashing, false);
        SetDashInvincibleLayer(owner, useInvincibleLayerWhileDashing, false);
    }

    private void SetEnemyCollisionIgnoredForDash(GameObject owner, bool enabled, bool ignored)
    {
        if (!enabled || owner == null)
            return;

        if (_enemyLayer < 0 || _enemyLayer > 31)
            return;

        if (ignored)
        {
            if (_isIgnoringEnemyCollision)
                return;

            _dashCollisionPlayerLayer = owner.layer;
            if (_dashCollisionPlayerLayer < 0 || _dashCollisionPlayerLayer > 31)
                return;

            CollisionPolicyService.SetIgnoreLayerCollision(_dashCollisionPlayerLayer, _enemyLayer, true);
            _isIgnoringEnemyCollision = true;
            return;
        }

        if (!_isIgnoringEnemyCollision)
            return;

        if (_dashCollisionPlayerLayer >= 0 && _dashCollisionPlayerLayer <= 31)
            CollisionPolicyService.SetIgnoreLayerCollision(_dashCollisionPlayerLayer, _enemyLayer, false);

        _dashCollisionPlayerLayer = -1;
        _isIgnoringEnemyCollision = false;
    }

    private void SetDashInvincibleLayer(GameObject owner, bool enabled, bool active)
    {
        if (!enabled || owner == null)
            return;

        if (_dashInvincibleLayer < 0 || _dashInvincibleLayer > 31)
            return;

        if (active)
        {
            if (_isDashLayerOverridden)
                return;

            _dashOriginalLayer = owner.layer;
            if (_dashOriginalLayer == _dashInvincibleLayer)
                return;

            owner.layer = _dashInvincibleLayer;
            _isDashLayerOverridden = true;
            return;
        }

        if (!_isDashLayerOverridden)
            return;

        if (_dashOriginalLayer >= 0 && _dashOriginalLayer <= 31)
            owner.layer = _dashOriginalLayer;

        _dashOriginalLayer = -1;
        _isDashLayerOverridden = false;
    }
}
