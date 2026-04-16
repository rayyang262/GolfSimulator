using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pairs each StatCardToggle (inside the phone's Display Library)
/// with a widget on the main screen-space Canvas.
/// When a card turns on, its widget shows. When it turns off, it hides.
/// </summary>
public class StatDisplayManager : MonoBehaviour
{
    [Serializable]
    public class Binding
    {
        [Tooltip("A stat card inside the DisplayLibraryOverlay (phone UI).")]
        public StatCardToggle card;

        [Tooltip("The widget on the main screen Canvas that corresponds to this card.")]
        public GameObject screenWidget;
    }

    [Tooltip("One entry per stat (FG, SQ, RS, DL).")]
    public List<Binding> bindings = new List<Binding>();

    void Start()
    {
        foreach (var b in bindings)
        {
            if (b == null || b.card == null) continue;

            GameObject widget = b.screenWidget;
            if (widget != null)
                widget.SetActive(b.card.IsActive);

            b.card.OnStateChanged += (active) =>
            {
                if (widget != null)
                    widget.SetActive(active);
            };
        }
    }
}
