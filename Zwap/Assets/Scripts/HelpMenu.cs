using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A browsable, swipeable help menu that explains the game's controls one page at a time.
///
/// Unlike <see cref="HelpManager"/> (which auto-shows a one-time popup the first time each
/// control appears mid-game), this menu is opened on demand from the pause menu's Help button
/// and lets the player page through every control at their own pace.
///
/// How it works:
///   - This script lives on the help panel's ROOT object (the full-screen panel that also acts
///     as the swipe surface). That root starts inactive; the pause menu's Help button turns it
///     on with a plain GameObject.SetActive(true) — no extra code needed. OnEnable then resets
///     to the first page.
///   - "pages" is your list of pre-made panels (one per control). Exactly one is visible at a
///     time. Design each panel yourself in the Editor.
///   - Navigation: horizontal swipe (touch OR mouse, via the EventSystem so it keeps working
///     while the game is frozen at Time.timeScale = 0), plus optional prev/next arrow buttons
///     and an auto-generated row of page dots.
///   - The Back/Close button calls CloseMenu(), which just deactivates this root.
///
/// Setup notes:
///   - This object needs a Graphic that is a raycast target (e.g. a full-screen Image, even a
///     transparent one) so the drag events fire across the whole panel.
///   - Put the page panels, arrows and dots as children so they turn off with the root.
/// </summary>
public class HelpMenu : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Pause Menu")]
    [Tooltip("The pause menu panel (PauseManager's pauseMenuUI). It is hidden while the help " +
             "menu is open and shown again when the player taps Back. The game stays paused " +
             "either way — this only swaps which panel is visible, never Time.timeScale.")]
    [SerializeField] private GameObject pauseMenuUI;

    [Tooltip("The separate pause/resume button. It is disabled while the help menu is open so " +
             "the player can't toggle pause behind the help menu, and re-enabled on Back.")]
    [SerializeField] private GameObject pauseButton;

    [Tooltip("Extra objects to hide while the help menu is open and show again on Back — e.g. " +
             "the on-screen Controls interfaces (Arrow Keys, Joystick, Sliders, ...). Each is " +
             "restored to active on close; the individual control that was showing stays the " +
             "one showing, because hiding the parent doesn't change its children's own state.")]
    [SerializeField] private GameObject[] hideWhileOpen;

    [Header("Pages")]
    [Tooltip("One panel per control, in the order they should appear. Exactly one is shown at " +
             "a time. Design each panel yourself in the Editor.")]
    [SerializeField] private GameObject[] pages;

    [Tooltip("Which control each page explains, parallel to Pages (same length, same order). " +
             "When the menu opens it jumps to the page whose control matches the one that is " +
             "currently active (ControlSwitcher.CurrentControl). Leave empty, or set a page's " +
             "entry to a control that isn't active, and the menu just opens on the first page. " +
             "The player can still swipe to any page from there.")]
    [SerializeField] private ControlType[] pageControls;

    [Tooltip("Parallel to Pages: tick the entry for a page that explains the INVERTED version of " +
             "its control (e.g. the inverse-Joystick screen). When the active control is inverted, " +
             "the menu opens on its ticked inverse page; if that control has no inverse page, it " +
             "falls back to the control's normal page. Leave every entry unticked if no page is an " +
             "inverse screen. Shorter than Pages is fine — missing entries count as not-inverted.")]
    [SerializeField] private bool[] pageInverted;

    [Header("Arrows (optional)")]
    [Tooltip("Goes to the previous page. Leave empty if you only want swipe + dots.")]
    [SerializeField] private Button prevButton;

    [Tooltip("Goes to the next page. Leave empty if you only want swipe + dots.")]
    [SerializeField] private Button nextButton;

    [Tooltip("When on, paging past the last page wraps to the first (and vice-versa). When off, " +
             "the arrows are disabled at the ends.")]
    [SerializeField] private bool wrapAround = false;

    [Header("Dots (optional)")]
    [Tooltip("Parent the generated page dots are placed under (usually a Horizontal Layout Group). " +
             "Leave empty to skip dots.")]
    [SerializeField] private RectTransform dotsParent;

    [Tooltip("Prefab instantiated once per page under Dots Parent. Needs an Image on its root.")]
    [SerializeField] private GameObject dotPrefab;

    [Tooltip("Tint for the dot of the current page.")]
    [SerializeField] private Color activeDotColor = Color.white;

    [Tooltip("Tint for the dots of the other pages.")]
    [SerializeField] private Color inactiveDotColor = new Color(1f, 1f, 1f, 0.4f);

    [Tooltip("Optional. If both sprites are set, dots swap sprite instead of just tint.")]
    [SerializeField] private Sprite activeDotSprite;
    [SerializeField] private Sprite inactiveDotSprite;

    [Header("Swipe")]
    [Tooltip("Minimum horizontal drag, in pixels, that counts as a swipe. Shorter drags are " +
             "ignored so a tap or a tiny wobble doesn't flip the page.")]
    [SerializeField] private float minSwipeDistance = 75f;

    // The page currently shown. Kept in range [0, pages.Length - 1] at all times.
    private int currentPage;

    // Where the current drag started, so we can measure how far it travelled on release.
    private Vector2 dragStartPosition;

    // The dot Images we generated, one per page, parallel to "pages".
    private readonly List<Image> dots = new List<Image>();

    private void Awake()
    {
        BuildDots();
    }

    // Reset to the first page every time the menu is opened, so it never reopens mid-way
    // through where the player last left off. Also hide the pause menu behind us, so only the
    // help menu shows (the game stays paused — we never touch Time.timeScale here).
    private void OnEnable()
    {
        ShowPage(GetStartPage());

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        if (pauseButton != null)
            pauseButton.SetActive(false);

        SetHiddenObjects(false);
    }

    // Picks the page to open on, matching both the active control and whether it is inverted:
    //   1. If the active control is inverted, prefer its inverse page (control matches AND the
    //      page is ticked inverted).
    //   2. Otherwise (or if no inverse page exists for it), use the control's normal page
    //      (control matches AND the page is NOT ticked inverted).
    //   3. Fall back to the first page whenever we can't resolve a match — no ControlSwitcher in
    //      the scene, no pageControls set up, or the active control simply has no page listed.
    // The player can always swipe from wherever we land, so a fallback is harmless.
    private int GetStartPage()
    {
        if (ControlSwitcher.Instance == null || pageControls == null || pages == null)
            return 0;

        ControlType current = ControlSwitcher.Instance.CurrentControl;
        bool inverted = ControlSwitcher.Instance.IsInverted;

        // When inverted, look for the matching inverse page first.
        if (inverted)
        {
            int invertedPage = FindPage(current, wantInverted: true);
            if (invertedPage >= 0)
                return invertedPage;
        }

        // Normal page for this control (also the fallback when an inverse page wasn't found).
        int normalPage = FindPage(current, wantInverted: false);
        return normalPage >= 0 ? normalPage : 0;
    }

    // Returns the first page whose control matches and whose inverted flag equals wantInverted,
    // or -1 if none. Scans only indices valid in Pages, and treats a short/absent pageInverted
    // array as "not inverted" for the trailing entries.
    private int FindPage(ControlType control, bool wantInverted)
    {
        int count = Mathf.Min(pageControls.Length, pages.Length);
        for (int i = 0; i < count; i++)
        {
            bool isInverted = pageInverted != null && i < pageInverted.Length && pageInverted[i];
            if (pageControls[i] == control && isInverted == wantInverted)
                return i;
        }

        return -1;
    }

    // Called by the Next arrow's OnClick (and by a right-to-left swipe).
    public void NextPage()
    {
        Move(+1);
    }

    // Called by the Prev arrow's OnClick (and by a left-to-right swipe).
    public void PrevPage()
    {
        Move(-1);
    }

    // Called by the Back/Close button's OnClick. Reopens the pause menu, then hides the whole
    // help menu. The game remains paused throughout (Time.timeScale is untouched).
    public void CloseMenu()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

        if (pauseButton != null)
            pauseButton.SetActive(true);

        SetHiddenObjects(true);

        gameObject.SetActive(false);
    }

    // Turns every "hide while open" object on or off together. Restoring them to active leaves
    // their own children untouched, so whichever control was showing is still the one showing.
    private void SetHiddenObjects(bool visible)
    {
        if (hideWhileOpen == null)
            return;

        foreach (var obj in hideWhileOpen)
        {
            if (obj != null)
                obj.SetActive(visible);
        }
    }

    private void Move(int direction)
    {
        if (pages == null || pages.Length == 0)
            return;

        int target = currentPage + direction;

        if (wrapAround)
        {
            // Wrap using a modulo that stays positive for negative targets.
            target = ((target % pages.Length) + pages.Length) % pages.Length;
        }
        else
        {
            // Clamp: paging past an end simply stays put.
            target = Mathf.Clamp(target, 0, pages.Length - 1);
        }

        ShowPage(target);
    }

    // Single source of truth for what's visible: activates exactly the target page, hides the
    // rest, refreshes the dots, and updates arrow interactability.
    private void ShowPage(int index)
    {
        if (pages == null || pages.Length == 0)
            return;

        currentPage = Mathf.Clamp(index, 0, pages.Length - 1);

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == currentPage);
        }

        RefreshDots();
        RefreshArrows();
    }

    private void RefreshArrows()
    {
        if (pages == null || pages.Length == 0)
            return;

        // When wrapping, the arrows are always usable. Otherwise they grey out at the ends.
        if (prevButton != null)
            prevButton.interactable = wrapAround || currentPage > 0;

        if (nextButton != null)
            nextButton.interactable = wrapAround || currentPage < pages.Length - 1;
    }

    private void BuildDots()
    {
        dots.Clear();

        if (dotsParent == null || dotPrefab == null || pages == null)
            return;

        // Clear any leftover dots from a previous build or Editor placeholders.
        for (int i = dotsParent.childCount - 1; i >= 0; i--)
            Destroy(dotsParent.GetChild(i).gameObject);

        for (int i = 0; i < pages.Length; i++)
        {
            GameObject dot = Instantiate(dotPrefab, dotsParent);
            dot.SetActive(true);

            Image image = dot.GetComponent<Image>();
            if (image != null)
                dots.Add(image);
        }
    }

    private void RefreshDots()
    {
        for (int i = 0; i < dots.Count; i++)
        {
            Image dot = dots[i];
            if (dot == null)
                continue;

            bool isActive = i == currentPage;

            // Swap sprite when both are provided; otherwise just recolour.
            if (activeDotSprite != null && inactiveDotSprite != null)
                dot.sprite = isActive ? activeDotSprite : inactiveDotSprite;

            dot.color = isActive ? activeDotColor : inactiveDotColor;
        }
    }

    // --- Swipe handling (works with touch and mouse via the EventSystem) ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPosition = eventData.position;
    }

    // Required so Unity's input module recognises this object as a drag target — without an
    // IDragHandler it never sets pointerDrag, and OnBeginDrag/OnEndDrag would never fire. We
    // don't need per-frame drag logic, so this is intentionally empty.
    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float horizontal = eventData.position.x - dragStartPosition.x;

        // Ignore drags that are too short, or that are mostly vertical.
        if (Mathf.Abs(horizontal) < minSwipeDistance)
            return;
        if (Mathf.Abs(horizontal) < Mathf.Abs(eventData.position.y - dragStartPosition.y))
            return;

        // Swipe left (finger moves right-to-left) → next page, like turning a page forward.
        if (horizontal < 0f)
            NextPage();
        else
            PrevPage();
    }
}
