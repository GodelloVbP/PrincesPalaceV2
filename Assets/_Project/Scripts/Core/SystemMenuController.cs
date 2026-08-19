using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Drives the overarching menu: open, close, and which tab is showing.
    //
    // A SKELETON. It switches panes and nothing else, because the panes are
    // empty -- the point is that the design pass can fill one without touching
    // any of this.
    public class SystemMenuController : MonoBehaviour
    {
        [SerializeField] internal GameObject panel;
        [SerializeField] internal Button[] tabButtons;
        [SerializeField] internal GameObject[] tabUnderlines;
        [SerializeField] internal GameObject[] panes;

        // Panels that own Escape while they are open.
        //
        // Wired per scene rather than found by name: the fight and the map put
        // the character sheet on Escape already, and a second listener that
        // opened this menu on top of a sheet the player was trying to close
        // would be a bug the player reports as "Escape does the wrong thing".
        // Empty in scenes that have no such panel.
        [SerializeField] internal GameObject[] escapeConsumers;

        private bool _wired;
        private int _selected;

        // Whether anything has chosen a tab yet.
        //
        // Start() runs on the frame AFTER the object is activated, so a caller
        // that did Open() then Select(2) in one breath had its choice silently
        // reset to 0 a frame later -- which is how the first screenshot of this
        // menu came out showing the Options pane with the Character tab
        // underlined. The default is a fallback, not an override.
        private bool _chosen;

        public bool IsOpen => panel != null && panel.activeSelf;

        public int SelectedIndex => _selected;

        private void Start()
        {
            Wire();
            if (!_chosen) Select(0);
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (tabButtons == null) return;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => Select(index));
            }
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // Closing beats opening: if this menu is already up, Escape is
            // unambiguously about it.
            if (IsOpen)
            {
                Close();
                return;
            }

            if (SomethingElseOwnsEscape()) return;
            Open();
        }

        private bool SomethingElseOwnsEscape()
        {
            if (escapeConsumers == null) return false;

            foreach (var consumer in escapeConsumers)
            {
                if (consumer != null && consumer.activeInHierarchy) return true;
            }

            return false;
        }

        public void Open()
        {
            Wire();
            SetActive(panel, true);
        }

        public void Close() => SetActive(panel, false);

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Select(SystemMenuTab tab) => Select(SystemMenuTabs.IndexOf(tab));

        // Exactly one pane and one underline live at a time.
        public void Select(int index)
        {
            if (panes == null || panes.Length == 0) return;

            _chosen = true;
            _selected = index < 0 ? 0 : index >= panes.Length ? panes.Length - 1 : index;

            for (int i = 0; i < panes.Length; i++)
            {
                SetActive(panes[i], i == _selected);
                if (tabUnderlines != null && i < tabUnderlines.Length)
                {
                    SetActive(tabUnderlines[i], i == _selected);
                }
            }
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
