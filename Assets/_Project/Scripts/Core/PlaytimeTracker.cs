using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrincesPalace
{
    // Wall-clock time added to the ACTIVE slot's save, ticked continuously
    // rather than measured between saves.
    //
    // Self-bootstrapping, the same shape CursorController and SoundController
    // already use: it fires before any scene loads, so no scene needs to know
    // it exists and no wired reference can go missing or drift out of a scene
    // that forgets to carry it.
    //
    // NOT COUNTED AT THE MAIN MENU. SaveSlotManager.CurrentSlot defaults to 0
    // before a player has chosen anything, so ticking unconditionally would
    // silently pile up idle main-menu time onto whichever slot the cache
    // happens to be pointed at — wrong for every slot but the one somebody
    // actually opened. Playing means being IN a slot's world: Hub, Map, Fight
    // or Talents, never the menu that chooses between them.
    //
    // FLUSHED INTO THE CACHE EVERY FRAME, not on a timer. SaveSlotManager
    // already caches one SaveData in memory, and every other system that
    // changes it — combat rewards, purchases, the reward track — writes it to
    // disk on its own schedule. This only has to keep the in-memory number
    // current, so whichever save happens next carries an honest total. What a
    // crash between saves costs is, at most, the seconds since the last one —
    // the same exposure every other unsaved change on this save already has.
    public class PlaytimeTracker : MonoBehaviour
    {
        private static PlaytimeTracker _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("PlaytimeTracker");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PlaytimeTracker>();
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == Navigation.MainMenu) return;

            SaveSlotManager.CurrentSave.totalPlaySeconds += Time.unscaledDeltaTime;
        }
    }
}
