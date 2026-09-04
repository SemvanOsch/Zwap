using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchControls : MonoBehaviour
{
    [SerializeField] private Transform upTransform;
    [SerializeField] private Transform downTransform;
    [SerializeField] private Transform leftTransform;
    [SerializeField] private Transform rightTransform;
    
    public PlayerStart playerStart;
    private bool isInversed = false;

    public void ToggleInverse()
    {
        isInversed = !isInversed;

        upTransform.rotation    = Quaternion.Euler(0, 0, isInversed ? 180 : 0);
        downTransform.rotation  = Quaternion.Euler(0, 0, isInversed ? 0 : 180);
        leftTransform.rotation  = Quaternion.Euler(0, 0, isInversed ? 270 : 90);
        rightTransform.rotation = Quaternion.Euler(0, 0, isInversed ? 90 : 270);
    }
    
    public void SetNormal()
    {
        isInversed = false;

        upTransform.rotation    = Quaternion.Euler(0, 0, 0);
        downTransform.rotation  = Quaternion.Euler(0, 0, 180);
        leftTransform.rotation  = Quaternion.Euler(0, 0, 90);
        rightTransform.rotation = Quaternion.Euler(0, 0, 270);
    }

    private Vector2 ApplyInverse(Vector2 dir)
    {
        return isInversed ? -dir : dir;
    }
    

    public void OnUpPress()      { playerStart.AddInput(ApplyInverse(Vector2.up)); }
    public void OnUpRelease()    { playerStart.RemoveInput(ApplyInverse(Vector2.up)); }

    public void OnDownPress()    { playerStart.AddInput(ApplyInverse(Vector2.down)); }
    public void OnDownRelease()  { playerStart.RemoveInput(ApplyInverse(Vector2.down)); }

    public void OnLeftPress()    { playerStart.AddInput(ApplyInverse(Vector2.left)); }
    public void OnLeftRelease()  { playerStart.RemoveInput(ApplyInverse(Vector2.left)); }

    public void OnRightPress()   { playerStart.AddInput(ApplyInverse(Vector2.right)); }
    public void OnRightRelease() { playerStart.RemoveInput(ApplyInverse(Vector2.right)); }
}