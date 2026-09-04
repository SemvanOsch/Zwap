using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchControls : MonoBehaviour
{
    public PlayerStart playerStart;
    private bool isInversed = false;

    public void ToggleInverse()
    {
        isInversed = !isInversed;
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