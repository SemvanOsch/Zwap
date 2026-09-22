using UnityEngine;

/// <summary>
/// Sits on a child GameObject holding the swipe's trigger collider, and simply
/// forwards trigger hits back to the owning Bear. Kept as its own tiny script
/// (rather than putting the collider directly on the Bear) so the hitbox can be
/// sized/positioned independently of the bear's own sprite/animation transform,
/// and so multiple overlapping colliders on the bear don't get confused about
/// which one fired.
/// </summary>
public class BearSwipeHitbox : MonoBehaviour
{
    [SerializeField] private Bear owner;

    private void Reset()
    {
        // Auto-fills when you add this component in the Editor, if it's a
        // child of the Bear - saves a manual drag most of the time.
        owner = GetComponentInParent<Bear>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null)
            owner.HandleSwipeHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Also forward ongoing overlaps, not just the initial enter. This matters
        // because we deliberately never enable/disable this collider (see Bear's
        // comments on isAttackLethalNow) - if the player is already standing
        // inside the hitbox's area the moment the swipe turns lethal, Unity will
        // NOT fire OnTriggerEnter2D for them (it only fires on the transition
        // into overlap, not on a state flag changing) - Stay covers that case.
        if (owner != null)
            owner.HandleSwipeHit(other);
    }
}