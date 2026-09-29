using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // HoldToConfirm's own Submit poll: pad A and Enter hold any hold button
    // that is the selected object. Ticked by hand with a scripted Submit level,
    // so no case waits out real time.
    public class HoldToConfirmSubmitTests
    {
        private GameObject _es;
        private GameObject _button;
        private HoldToConfirm _hold;
        private bool _submit;
        private int _completed;

        [SetUp]
        public void Build()
        {
            _es = new GameObject("es", typeof(EventSystem));
            _button = new GameObject("hold");
            _hold = _button.AddComponent<HoldToConfirm>();
            _hold.Completed = () => _completed++;
            _submit = false;
            _completed = 0;
            HoldToConfirm.SubmitLevelOverride = () => _submit;
            _hold.Tick(0f);
        }

        [TearDown]
        public void Teardown()
        {
            HoldToConfirm.SubmitLevelOverride = null;
            Object.Destroy(_button);
            Object.Destroy(_es);
        }

        private void Select() => EventSystem.current.SetSelectedGameObject(_button);

        [Test]
        public void SecondsIsTwelveTenths()
        {
            Assert.AreEqual(1.2f, _hold.Seconds, 0.0001f);
        }

        [Test]
        public void HoldingSubmitForTheFullDurationCompletesOnce()
        {
            Select();
            _submit = true;
            _hold.Tick(0f);
            Assert.IsTrue(_hold.Holding);

            _hold.Tick(0.6f);
            Assert.AreEqual(0.5f, _hold.Progress01, 0.001f);
            _hold.Tick(0.6f);
            Assert.AreEqual(1, _completed);
            Assert.AreEqual(0f, _hold.Progress01, 0.0001f);

            // Submit still down: the finished hold does not restart.
            _hold.Tick(1.3f);
            Assert.AreEqual(1, _completed);
        }

        [Test]
        public void ReleasingAtOneSecondDoesNotCompleteAndResetsProgress()
        {
            Select();
            _submit = true;
            _hold.Tick(0f);
            _hold.Tick(1.0f);
            Assert.Greater(_hold.Progress01, 0.8f);

            _submit = false;
            _hold.Tick(0.3f);
            Assert.AreEqual(0, _completed);
            Assert.AreEqual(0f, _hold.Progress01, 0.0001f);
            Assert.IsFalse(_hold.Holding);
        }

        [Test]
        public void SelectionMovingAwayMidHoldCancels()
        {
            Select();
            _submit = true;
            _hold.Tick(0f);
            _hold.Tick(0.5f);
            Assert.IsTrue(_hold.Holding);

            EventSystem.current.SetSelectedGameObject(null);
            _hold.Tick(0.9f);
            Assert.AreEqual(0, _completed);
            Assert.AreEqual(0f, _hold.Progress01, 0.0001f);
        }

        [Test]
        public void ASubmitAlreadyDownWhenSelectedDoesNotStartAHold()
        {
            _submit = true;
            _hold.Tick(0f);
            Select();
            _hold.Tick(2.0f);
            Assert.IsFalse(_hold.Holding);
            Assert.AreEqual(0, _completed);

            // Released and pressed afresh, it does.
            _submit = false;
            _hold.Tick(0f);
            _submit = true;
            _hold.Tick(0f);
            Assert.IsTrue(_hold.Holding);
        }

        [Test]
        public void SubmitIsIgnoredWhileAnotherObjectIsSelected()
        {
            var other = new GameObject("other");
            EventSystem.current.SetSelectedGameObject(other);
            _submit = true;
            _hold.Tick(0f);
            _hold.Tick(2.0f);
            Assert.IsFalse(_hold.Holding);
            Object.Destroy(other);
        }

        [Test]
        public void APointerHoldIsNotCancelledBySubmitPollAndCompletesOnItsOwn()
        {
            Select();
            _hold.OnPointerDown(new PointerEventData(EventSystem.current));
            _hold.Tick(0.6f);
            Assert.IsTrue(_hold.Holding, "no Submit was ever down; the poll cancelled a pointer hold");
            _hold.Tick(0.6f);
            Assert.AreEqual(1, _completed);
        }

        [Test]
        public void APointerPressTakesOverASubmitHold()
        {
            Select();
            _submit = true;
            _hold.Tick(0f);
            _hold.Tick(0.5f);
            _hold.OnPointerDown(new PointerEventData(EventSystem.current));

            _submit = false;
            _hold.Tick(0.1f);
            Assert.IsTrue(_hold.Holding);
        }

        [Test]
        public void PointerUpStillCancels()
        {
            _hold.OnPointerDown(new PointerEventData(EventSystem.current));
            _hold.Tick(0.5f);
            _hold.OnPointerUp(new PointerEventData(EventSystem.current));
            Assert.IsFalse(_hold.Holding);
            Assert.AreEqual(0f, _hold.Progress01, 0.0001f);
        }
    }
}
