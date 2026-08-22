using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Ambience;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Talents;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The constellations, driven.
    //
    // Every rule about what can be kindled lives in TalentPage, in Domain, with
    // its own tests. This decides nothing -- it reads the save, asks Domain, and
    // paints. The one thing it genuinely owns is the SLIDE, because that is time
    // and time needs a frame.
    public class TalentController : MonoBehaviour
    {
        [SerializeField] internal RectTransform sky;
        [SerializeField] internal Button[] orbs;

        // The lit layer of each edge, and the slot each one arrives at. Two
        // parallel arrays rather than a lookup: the screen built them in one
        // pass and UiCountAudit can hold their lengths to each other, which a
        // dictionary assembled at runtime could not be checked against.
        [SerializeField] internal TalentNodeInvestReveal[] orbReveals;
        [SerializeField] internal GameObject[] edgeGlows;
        [SerializeField] internal int[] edgeChildSlots;
        [SerializeField] internal Image[] orbGlows;
        [SerializeField] internal TMP_Text characterName;
        [SerializeField] internal TMP_Text pathName;
        [SerializeField] internal TMP_Text emberCount;
        [SerializeField] internal GameObject detailPlate;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailBody;
        [SerializeField] internal TMP_Text investLabel;
        [SerializeField] internal Button investButton;

        // Level 20 of the reward track. See Respec().
        [SerializeField] internal Button respecButton;
        [SerializeField] internal Button prevPathButton;
        [SerializeField] internal Button nextPathButton;
        [SerializeField] internal Button prevCharacterButton;
        [SerializeField] internal Button nextCharacterButton;
        [SerializeField] internal Button backButton;

        // Orb states, graded so the tree reads at a glance rather than needing
        // to be studied: yours is bright, reachable is warm, everything else is
        // barely there.
        private static readonly Color OrbTaken = new Color(1f, 0.85f, 0.55f, 1f);
        private static readonly Color OrbReachable = new Color(0.95f, 0.78f, 0.45f, 0.75f);
        private static readonly Color OrbDistant = new Color(0.45f, 0.40f, 0.58f, 0.5f);
        private static readonly Color GlowTaken = new Color(1f, 0.80f, 0.45f, 0.55f);
        private static readonly Color GlowReachable = new Color(1f, 0.80f, 0.45f, 0.16f);
        private static readonly Color GlowOff = new Color(1f, 0.80f, 0.45f, 0f);

        private int _path;
        private int _character;
        private int _selectedSlot = -1;
        private bool _wired;

        // The slide, in progress. Held rather than run as a coroutine so a
        // second press mid-slide retargets it instead of starting a second one
        // that fights the first for the same rect.
        private float _slideElapsed;
        private int _slideFrom;
        private bool _sliding;

        private void Start()
        {
            Wire();
            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < orbs.Length; i++)
            {
                int index = i;
                orbs[i].onClick.AddListener(() => OnOrbPressed(index));
            }

            prevPathButton.onClick.AddListener(() => StepPath(-1));
            nextPathButton.onClick.AddListener(() => StepPath(1));
            prevCharacterButton.onClick.AddListener(() => StepCharacter(-1));
            nextCharacterButton.onClick.AddListener(() => StepCharacter(1));
            investButton.onClick.AddListener(Kindle);
            if (respecButton != null) respecButton.onClick.AddListener(Respec);
            backButton.onClick.AddListener(() => Navigation.Go(Navigation.Hub));
        }

        // ---- who and where ---------------------------------------------------

        private List<Character> Roster =>
            SaveSlotManager.CurrentSave?.roster ?? new List<Character>();

        private Character Current =>
            Roster.Count == 0 ? null : Roster[Mathf.Clamp(_character, 0, Roster.Count - 1)];

        private HashSet<string> Unlocked =>
            new HashSet<string>(Current?.unlockedTalentIds ?? new List<string>());

        // THE SELECTED CHARACTER'S embers, not the profile's. Reading a shared
        // pool here is what let a character you never fielded be kindled to the
        // top of their tree out of someone else's earnings.
        private int Embers => Current?.embers ?? 0;

        // ---- input -----------------------------------------------------------

        private void StepPath(int direction)
        {
            int next = ConstellationLayout.Step(_path, direction, TalentPage.PathCount);
            if (next == _path) return;

            // Slid from wherever it currently IS, not from the last settled
            // page: pressing twice quickly should carry on from mid-flight
            // rather than snapping back and starting again.
            _slideFrom = _path;
            _path = next;
            _slideElapsed = 0f;
            _sliding = true;

            _selectedSlot = -1;
            Refresh();
        }

        private void StepCharacter(int direction)
        {
            if (Roster.Count <= 1) return;

            _character = ConstellationLayout.Step(_character, direction, Roster.Count);

            // A different character's constellations are a different tree, so
            // the selection cannot survive the switch -- it would describe an
            // orb that is no longer under the cursor.
            _selectedSlot = -1;
            Refresh();
        }

        private void OnOrbPressed(int index)
        {
            int path = index / TalentScreen.OrbCount;
            int slot = index % TalentScreen.OrbCount;

            // Only the page being LOOKED AT is clickable. The other two are
            // sitting off-screen with live buttons on them, and a stray click
            // landing there would kindle something the player cannot see.
            if (path != _path) return;

            _selectedSlot = slot;
            Refresh();
        }

        // THE CHARACTER'S OWN TREE, resolved from content.
        //
        // Rebuilt when the character changes rather than per repaint: it walks
        // every talent the character has and costs each one, which is not work
        // to repeat on every hover. Cached against the id it was built for, so
        // switching character and switching back cannot leave a stale tree
        // describing somebody else's talents.
        private TalentTree _tree = TalentTree.None;
        private string _treeFor;

        private TalentTree Tree
        {
            get
            {
                var character = Current;
                if (character == null) return TalentTree.None;

                if (_treeFor == character.definitionId) return _tree;

                _tree = BuildTree(character);
                _treeFor = character.definitionId;
                return _tree;
            }
        }

        // column -> path and row -> slot, which is what the content has always
        // meant by those fields: TalentEntryResolver refuses a row outside
        // 0..20, and the skeleton is 21 slots.
        private static TalentTree BuildTree(Character character)
        {
            var tree = new TalentTree();

            foreach (var talent in ContentDatabase.TalentsFor(character))
            {
                if (talent == null) continue;

                tree.Set(talent.column, talent.row, new TalentSlot(
                    talent.id,
                    talent.displayName,
                    talent.description,
                    ContentDatabase.OrbCost(talent)));
            }

            return tree;
        }

        private void Kindle()
        {
            var character = Current;
            if (character == null || _selectedSlot < 0) return;

            var save = SaveSlotManager.CurrentSave;
            if (save == null) return;

            var tree = Tree;
            if (!TalentPage.CanInvest(tree, _path, _selectedSlot, Unlocked, Embers)) return;

            // THE CONTENT'S OWN ID, which is the entire point of this seam.
            // Everything that reads unlockedTalentIds -- the effective stats,
            // the ability scores, the combat effect set -- matches against the
            // ids in talents.json, and this used to write one it had invented.
            var taken = tree.At(_path, _selectedSlot);
            character.unlockedTalentIds.Add(taken.Id);
            character.embers -= taken.Cost;

            // PLAYED BEFORE THE REPAINT, and that ordering is the whole of it.
            //
            // Refresh below calls EnsureShown on every taken orb, which snaps a
            // reveal straight to full size. Starting the animation first means
            // this orb is already mid-Play by the time that runs, and
            // EnsureShown deliberately does nothing to a reveal in flight --
            // so the one node the player just kindled animates and the twenty
            // they kindled earlier do not.
            PlayRevealFor(_path, _selectedSlot);

            // Written immediately. A talent tree that loses a kindled orb to a
            // crash is the single least forgivable thing this screen could do.
            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        // Gives back everything this character has committed, so they can
        // spend it again. Level 20 of the reward track.
        //
        // BOTH CURRENCIES, because there are two: embers committed to orbs, and
        // stat points placed into ability scores. Character.Respec owns the
        // refund itself -- including the rule that event-taught skills are NOT
        // stripped -- and this supplies the one figure the model cannot compute
        // for itself: what the talents actually cost, which is a content
        // question (ContentDatabase.SpentBy walks OrbCost).
        //
        // The stat half currently refunds nothing in practice, and it is here
        // anyway: nothing in the game can SPEND a stat point yet
        // (AUDIT.md #53, Character.Invest has no production caller), so the
        // invested block is always zero. Written now rather than later because
        // the day a "+" appears on the dossier, a respec that quietly forgot
        // half its job is a bug nobody would look for.
        private void Respec()
        {
            var character = Current;
            if (character == null) return;

            // Earned, not free-for-all. The button is hidden below this level
            // too -- checked in both places because a hidden button is a
            // presentation fact and this is the rule.
            if (!RewardTrack.HasUnlocked(TrackReward.Respec, character.level)) return;

            character.Respec(ContentDatabase.SpentBy(character));

            // Written immediately, for the same reason Kindle writes
            // immediately: a tree that loses its state to a crash is the least
            // forgivable thing this screen can do, and that cuts both ways.
            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        // ---- painting --------------------------------------------------------

        public void Refresh()
        {
            var character = Current;
            if (character == null) return;

            characterName.SetContent(character.definitionId);
            emberCount.Set(UiStrings.TalentEmbers, Embers);

            // HIDDEN until earned, not greyed out -- the same call the
            // Reckoning's reroll makes. A disabled button for a reward the
            // player has never heard of reads as something broken rather than
            // as something unearned.
            //
            // Interactable only when there is something to give back, so
            // pressing it is never a no-op the player has to interpret.
            if (respecButton != null)
            {
                bool earned = RewardTrack.HasUnlocked(TrackReward.Respec, character.level);
                respecButton.SetShown(earned);
                if (earned)
                {
                    respecButton.interactable =
                        character.HasAnythingToRespec(ContentDatabase.SpentBy(character));
                }
            }

            var unlocked = Unlocked;
            pathName.Set(UiStrings.TalentPath, _path + 1, TalentPage.PathCount,
                TalentPage.SpentOn(Tree, _path, unlocked));

            PaintOrbs(unlocked);
            PaintDetail(unlocked);

            // HIDDEN, not dimmed, when there is nobody to page to.
            //
            // Dimming was right while the roster was five and a squad might
            // field fewer: the arrows would come back, so keeping the screen
            // the same shape was worth more than removing two greyed controls.
            // The roster is ONE character now and the other four were removed
            // rather than postponed, so a permanently disabled arrow is not a
            // control between uses -- it is an advertisement for characters
            // that do not exist, which is exactly what this project keeps
            // having to unpick.
            //
            // The shape argument still holds for a roster that grows: this
            // hides only at one, so a second character brings both back
            // without anything else changing.
            bool canPage = Roster.Count > 1;

            prevCharacterButton.interactable = canPage;
            nextCharacterButton.interactable = canPage;

            if (prevCharacterButton.gameObject.activeSelf != canPage)
            {
                prevCharacterButton.gameObject.SetActive(canPage);
            }
            if (nextCharacterButton.gameObject.activeSelf != canPage)
            {
                nextCharacterButton.gameObject.SetActive(canPage);
            }
        }

        private void PaintOrbs(HashSet<string> unlocked)
        {
            var tree = Tree;

            for (int path = 0; path < TalentPage.PathCount; path++)
            {
                for (int slot = 0; slot < TalentScreen.OrbCount; slot++)
                {
                    int index = TalentScreen.OrbIndex(path, slot);
                    if (index >= orbs.Length) continue;

                    var refusal = TalentPage.Evaluate(tree, path, slot, unlocked, Embers);
                    bool taken = refusal == TalentPage.Refusal.AlreadyTaken;
                    bool reachable = refusal == TalentPage.Refusal.None;

                    // AN ORB WITH NOTHING BEHIND IT IS NOT DRAWN. The sheep's
                    // third path is entirely unauthored, so this is 21 real
                    // slots rather than a defensive branch -- and an orb that
                    // can never be taken is worse than an absent one, because
                    // the player spends the screen working out why.
                    bool authored = refusal != TalentPage.Refusal.NotAuthored;
                    orbs[index].SetShown(authored);
                    if (!authored) continue;

                    var image = orbs[index].targetGraphic as Image;
                    if (image != null) image.color = taken ? OrbTaken : reachable ? OrbReachable : OrbDistant;

                    if (index < orbGlows.Length && orbGlows[index] != null)
                    {
                        orbGlows[index].color = taken ? GlowTaken : reachable ? GlowReachable : GlowOff;
                    }

                    // Everything stays PRESSABLE, including what cannot be
                    // afforded: pressing an orb selects it and the detail plate
                    // is where the refusal is explained. An uninteractable orb
                    // can only say "no" by doing nothing.
                    orbs[index].interactable = path == _path;

                    // Idempotent, and it must stay that way: this runs on every
                    // redraw -- a path change, a different character, selecting
                    // another orb -- and a reveal that restarted here would
                    // replay the whole tree's worth of flourishes every time
                    // the player moved the selection.
                    if (orbReveals != null && index < orbReveals.Length && orbReveals[index] != null)
                    {
                        if (taken) orbReveals[index].EnsureShown();
                        else orbReveals[index].HideInstantly();
                    }
                }
            }

            PaintEdges(unlocked);
        }

        // THE PATH BEHIND YOU LIGHTS UP.
        //
        // An edge is lit when its CHILD is invested -- the parent necessarily
        // already is, because that is what a prerequisite means, so the child
        // alone answers it and nothing here has to re-walk the skeleton.
        //
        // The edges were pure decoration before this: three paths' worth of
        // limbs that never changed whatever the player spent, so a tree with
        // twenty orbs invested looked exactly like an empty one apart from the
        // orbs themselves. The climb is the thing the screen is about.
        private void PaintEdges(HashSet<string> unlocked)
        {
            var tree = Tree;

            if (edgeGlows == null || edgeChildSlots == null) return;

            // FROM THE SKELETON, NOT FROM THE ARRAY'S LENGTH.
            //
            // This divided the array length by the path count to work out how
            // many edges a path has, which is only correct while the array is
            // exactly the right size -- and silently wrong in the worst way
            // when it is not. One short array and every edge shifts to a
            // different path, lighting connections on trees the player has
            // never touched, with nothing logged.
            //
            // TalentSkeleton.EdgesPerPath is the same number derived from the
            // graph itself, so a mismatch is now a disagreement between two
            // sources that can be SEEN rather than a quiet reinterpretation of
            // one. The same reason the layout derives its waist from the
            // skeleton instead of writing 4.
            int perPath = Domain.Talents.TalentSkeleton.EdgesPerPath;
            int count = Mathf.Min(edgeGlows.Length, edgeChildSlots.Length);

            if (perPath <= 0 || count < perPath * TalentPage.PathCount)
            {
                Debug.LogWarning(
                    $"[TalentController] The tree has {count} edges wired but the skeleton describes " +
                    $"{perPath * TalentPage.PathCount}. Leaving them dark rather than lighting the " +
                    "wrong ones.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (edgeGlows[i] == null) continue;

                // The edges are built path by path in the same order the orbs
                // are, so which path an edge belongs to is its index over the
                // per-path count.
                int path = i / perPath;
                int slot = edgeChildSlots[i];

                // An edge is lit when its CHILD is invested, and an edge into
                // an unauthored slot can never be.
                string id = tree.IdAt(path, slot);
                bool lit = !string.IsNullOrEmpty(id) && unlocked.Contains(id);
                edgeGlows[i].SetShown(lit);
            }
        }

        private void PlayRevealFor(int path, int slot)
        {
            if (orbReveals == null) return;

            int index = TalentScreen.OrbIndex(path, slot);
            if (index < 0 || index >= orbReveals.Length) return;

            if (orbReveals[index] != null) orbReveals[index].Play();
        }

                private void PaintDetail(HashSet<string> unlocked)
        {
            if (_selectedSlot < 0)
            {
                detailName.SetContent("");
                detailBody.SetContent("");
                // The plate goes with them. Blanking the text and leaving the
                // slab behind is how the screen ended up opening onto an empty
                // coloured rectangle -- the plate frames an answer, and with no
                // orb picked there is no question.
                detailPlate.SetShown(false);
                investButton.SetShown(false);
                return;
            }

            var tree = Tree;
            var here = tree.At(_path, _selectedSlot);
            var refusal = TalentPage.Evaluate(tree, _path, _selectedSlot, unlocked, Embers);

            // Nothing authored here means nothing to say about it. The orb is
            // not drawn either, so this is only reachable by a selection that
            // survived a path change.
            if (refusal == TalentPage.Refusal.NotAuthored)
            {
                detailPlate.SetShown(false);
                investButton.SetShown(false);
                return;
            }

            detailPlate.SetShown(true);
            investButton.SetShown(true);

            // THE TALENT'S OWN NAME AND WORDS. This printed
            // TalentSkeleton.Kind -- literally "NORMAL", "MERGE" or "CAP" --
            // because the screen had no way to reach the content at all.
            detailName.SetContent(here.Name);

            // The description, and the reason it cannot be taken underneath it
            // when there is one. A refusal replacing the description would
            // leave the player unable to read what they are being refused.
            string explain = Explain(refusal);
            detailBody.SetContent(string.IsNullOrEmpty(explain)
                ? here.Description
                : here.Description + "\n" + explain);

            investButton.interactable = refusal == TalentPage.Refusal.None;
            investLabel.Set(LabelFor(refusal));
        }

        // The refusal the player is told about is the FIRST one that applies,
        // in the order Domain ranks them -- being told "not enough Embers" for
        // an orb three tiers out of reach sends someone to farm a currency they
        // did not need.
        private static string Explain(TalentPage.Refusal refusal)
        {
            switch (refusal)
            {
                case TalentPage.Refusal.AlreadyTaken: return "Already kindled.";
                case TalentPage.Refusal.PrerequisiteMissing: return "Kindle the star before it first.";
                case TalentPage.Refusal.NotEnoughEmbers: return "Not enough Embers.";
                default: return "Ready to kindle.";
            }
        }

        private static UiString LabelFor(TalentPage.Refusal refusal)
        {
            switch (refusal)
            {
                case TalentPage.Refusal.AlreadyTaken: return UiStrings.TalentTaken;
                case TalentPage.Refusal.PrerequisiteMissing: return UiStrings.TalentLocked;
                case TalentPage.Refusal.NotEnoughEmbers: return UiStrings.TalentNoEmbers;
                default: return UiStrings.TalentInvest;
            }
        }

        // ---- the slide -------------------------------------------------------

        private void Update()
        {
            if (sky == null) return;

            if (!_sliding)
            {
                sky.anchoredPosition = new Vector2(SettledX(), sky.anchoredPosition.y);
                return;
            }

            _slideElapsed += Time.deltaTime;
            float progress = ConstellationLayout.SlideProgress(_slideElapsed);

            sky.anchoredPosition = new Vector2(
                ConstellationLayout.SlideOffset(_slideFrom, _path, progress),
                sky.anchoredPosition.y);

            if (progress >= 1f) _sliding = false;
        }

        private float SettledX() => ConstellationLayout.SlideOffset(_path, _path, 1f);
    }
}
