using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinyGardenKeeper.AR
{
    public enum CompanionType
    {
        None,
        Butterfly,
        Bird,
        Crow
    }

    /// <summary>
    /// Manages randomized sequential companion visits (1 active at a time).
    /// Handles visit durations, intermissions, and player tap-to-shoo interactions for the crow.
    /// </summary>
    public class CompanionVisitManager : MonoBehaviour
    {
        [Header("Companion References")]
        [SerializeField] private GameObject butterflyCompanion;
        [SerializeField] private GameObject birdCompanion;
        [SerializeField] private GameObject crowCompanion;

        [Header("Timings (Seconds)")]
        [Tooltip("How long a companion visits and circles the pot.")]
        [SerializeField] private float visitDuration = 18f;

        [Tooltip("Pause duration between companion visits.")]
        [SerializeField] private float intermissionDuration = 4f;

        [Tooltip("Delay before the very first companion visits.")]
        [SerializeField] private float initialDelay = 2f;

        // Current State
        private CompanionType activeCompanion = CompanionType.None;
        private Coroutine visitCycleRoutine;
        private Coroutine activeVisitTimer;
        private bool isRunning = false;

        public CompanionType ActiveCompanion => activeCompanion;
        public event Action<CompanionType> OnCompanionArrived;
        public event Action<CompanionType> OnCompanionDeparted;

        private void Awake()
        {
            // Ensure all companions start deactivated
            DeactivateAll();
        }

        private void OnEnable()
        {
            StartVisitCycle();
        }

        private void OnDisable()
        {
            StopVisitCycle();
        }

        public void StartVisitCycle()
        {
            if (isRunning) return;
            isRunning = true;
            visitCycleRoutine = StartCoroutine(VisitCycleLoop());
        }

        public void StopVisitCycle()
        {
            isRunning = false;
            if (visitCycleRoutine != null)
            {
                StopCoroutine(visitCycleRoutine);
                visitCycleRoutine = null;
            }
            if (activeVisitTimer != null)
            {
                StopCoroutine(activeVisitTimer);
                activeVisitTimer = null;
            }
            DeactivateAll();
            activeCompanion = CompanionType.None;
        }

        private IEnumerator VisitCycleLoop()
        {
            yield return new WaitForSeconds(initialDelay);

            CompanionType lastType = CompanionType.None;

            while (isRunning)
            {
                // Choose a random companion different from the last one
                CompanionType nextType = PickRandomCompanion(lastType);
                lastType = nextType;

                // Spawn and activate companion
                SpawnCompanion(nextType);

                // Wait for the visit duration (or until shooed away early)
                float timer = 0f;
                while (timer < visitDuration && activeCompanion == nextType)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }

                // Depart companion
                DepartActiveCompanion();

                // Intermission pause before next visit
                yield return new WaitForSeconds(intermissionDuration);
            }
        }

        private CompanionType PickRandomCompanion(CompanionType exclude)
        {
            CompanionType[] pool = { CompanionType.Butterfly, CompanionType.Bird, CompanionType.Crow };
            List<CompanionType> available = new List<CompanionType>();
            foreach (var c in pool)
            {
                if (c != exclude) available.Add(c);
            }

            int index = UnityEngine.Random.Range(0, available.Count);
            return available[index];
        }

        private void SpawnCompanion(CompanionType type)
        {
            DeactivateAll();
            activeCompanion = type;

            GameObject targetObj = GetCompanionObject(type);
            if (targetObj != null)
            {
                targetObj.SetActive(true);
            }

            OnCompanionArrived?.Invoke(type);
        }

        private void DepartActiveCompanion()
        {
            if (activeCompanion == CompanionType.None) return;

            CompanionType departed = activeCompanion;
            GameObject targetObj = GetCompanionObject(departed);

            if (targetObj != null)
            {
                targetObj.SetActive(false);
            }

            activeCompanion = CompanionType.None;
            OnCompanionDeparted?.Invoke(departed);
        }

        /// <summary>
        /// Called when the player taps the Crow to scare it away early.
        /// </summary>
        public void ShooCrow()
        {
            if (activeCompanion == CompanionType.Crow)
            {
                Debug.Log("[CompanionVisitManager] Crow was shooed away by player tap!");
                DepartActiveCompanion();
            }
        }

        private GameObject GetCompanionObject(CompanionType type)
        {
            switch (type)
            {
                case CompanionType.Butterfly: return butterflyCompanion;
                case CompanionType.Bird: return birdCompanion;
                case CompanionType.Crow: return crowCompanion;
                default: return null;
            }
        }

        private void DeactivateAll()
        {
            if (butterflyCompanion != null) butterflyCompanion.SetActive(false);
            if (birdCompanion != null) birdCompanion.SetActive(false);
            if (crowCompanion != null) crowCompanion.SetActive(false);
        }
    }
}
