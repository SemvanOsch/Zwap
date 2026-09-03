using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchControls : MonoBehaviour
{
    public PlayerStart playerStart;

    public void OnUpPress()    { Debug.Log("Up pressed"); playerStart.AddInput(Vector2.up); }
    public void OnUpRelease()  { Debug.Log("Up released"); playerStart.RemoveInput(Vector2.up); }

    public void OnDownPress()    { Debug.Log("Down pressed"); playerStart.AddInput(Vector2.down); }
    public void OnDownRelease()  { Debug.Log("Down released"); playerStart.RemoveInput(Vector2.down); }

    public void OnLeftPress()    { Debug.Log("Left pressed"); playerStart.AddInput(Vector2.left); }
    public void OnLeftRelease()  { Debug.Log("Left released"); playerStart.RemoveInput(Vector2.left); }

    public void OnRightPress()    { Debug.Log("Right pressed"); playerStart.AddInput(Vector2.right); }
    public void OnRightRelease()  { Debug.Log("Right released"); playerStart.RemoveInput(Vector2.right); }
}