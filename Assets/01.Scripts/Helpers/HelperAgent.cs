using System;
using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Helpers
{
    /// <summary>
    /// The visible half of one helper visit: fly in from off-screen, hover over the target petal,
    /// hand the actual harvest back to <see cref="HelperManager"/>, fly out, return to the pool.
    /// Owns no gameplay state — killing this object mid-flight costs at most one visit's yield.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class HelperAgent : MonoBehaviour
    {
        private enum Phase
        {
            FlyIn,
            Collect,
            FlyOut,
        }

        // Visual plumbing, not game tuning: how much the agent bobs while flying and hovering.
        private const float BobAmplitude = 0.05f;
        private const float BobFrequencyHz = 2.4f;

        private HelperData data;
        private PetalController targetPetal;
        private Vector3 entryPoint;
        private Vector3 exitPoint;
        private Vector3 lastKnownTargetPosition;
        private Vector3 flyOutStart;
        private Action<HelperData, PetalController> onCollect;
        private Action<HelperAgent> onFinished;

        private Phase phase;
        private float phaseTime;

        public void Launch(HelperData helperData, PetalController petal,
                           Vector3 entry, Vector3 exit,
                           Action<HelperData, PetalController> collectCallback,
                           Action<HelperAgent> finishedCallback)
        {
            data = helperData;
            targetPetal = petal;
            entryPoint = entry;
            exitPoint = exit;
            onCollect = collectCallback;
            onFinished = finishedCallback;

            lastKnownTargetPosition = TargetPosition();
            phase = Phase.FlyIn;
            phaseTime = 0f;
            transform.position = entry;
            transform.localScale = Vector3.one * data.AgentDiameter;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (data == null)
            {
                return;
            }

            phaseTime += Time.deltaTime;

            switch (phase)
            {
                case Phase.FlyIn:
                {
                    float t = Mathf.Clamp01(phaseTime / data.FlyDurationSeconds);
                    transform.position = Vector3.Lerp(entryPoint, TargetPosition(),
                        Mathf.SmoothStep(0f, 1f, t)) + Bob();
                    if (t >= 1f)
                    {
                        NextPhase(Phase.Collect);
                    }

                    break;
                }

                case Phase.Collect:
                {
                    transform.position = TargetPosition() + Bob();
                    if (phaseTime >= data.CollectDurationSeconds)
                    {
                        onCollect?.Invoke(data, targetPetal);
                        flyOutStart = transform.position;
                        NextPhase(Phase.FlyOut);
                    }

                    break;
                }

                case Phase.FlyOut:
                {
                    float t = Mathf.Clamp01(phaseTime / data.FlyDurationSeconds);
                    transform.position = Vector3.Lerp(flyOutStart, exitPoint,
                        Mathf.SmoothStep(0f, 1f, t)) + Bob();
                    if (t >= 1f)
                    {
                        onFinished?.Invoke(this);
                    }

                    break;
                }
            }
        }

        private void NextPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        /// <summary>
        /// Where to fly to. The petal can die mid-flight (a click or another helper finished it);
        /// the agent then heads to where it last saw it and the manager retargets the harvest.
        /// </summary>
        private Vector3 TargetPosition()
        {
            if (targetPetal != null)
            {
                // Hover slightly on the camera side of the petal so the disc never z-fights it.
                lastKnownTargetPosition = targetPetal.MeshRenderer.bounds.center + Vector3.back * 0.1f;
            }

            return lastKnownTargetPosition;
        }

        private Vector3 Bob()
        {
            return Vector3.up * (Mathf.Sin(Time.time * BobFrequencyHz * 2f * Mathf.PI) * BobAmplitude);
        }
    }
}
