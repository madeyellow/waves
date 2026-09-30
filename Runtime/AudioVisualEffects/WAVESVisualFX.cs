using System.Collections.Generic;
using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.AudioVisualEffects.Abstractions;
using UnityEngine;
using UnityEngine.VFX;

namespace MadeYellow.WAVES.AudioVisualEffects
{
    /// <summary>
    /// One shared world-space player per particle prefab and per visual effect asset.
    /// Particle bursts are restarted in place. Graph events carry their own position and angle.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MadeYellow/WAVES/WAVES Visual FX")]
    public sealed class WAVESVisualFX : MonoBehaviour, IWAVESVisualFX
    {
        static readonly int PositionId = Shader.PropertyToID("position");
        static readonly int AngleId = Shader.PropertyToID("angle");
        static readonly int PlayEventId = Shader.PropertyToID("OnPlay");

        sealed class ParticleVoice
        {
            public Transform Root;
            public ParticleSystem[] Systems;
        }

        sealed class GraphVoice
        {
            public VisualEffect Effect;
            public VFXEventAttribute Attribute;
        }

        readonly Dictionary<int, ParticleVoice> _particles = new Dictionary<int, ParticleVoice>();
        readonly Dictionary<int, GraphVoice> _graphs = new Dictionary<int, GraphVoice>();

        /// <inheritdoc />
        public void PlayParticles(
            int emitterId,
            ActorProfile actor,
            Vector3 position,
            Quaternion rotation,
            float cullingDistance,
            ParticleSystem particles)
        {
            if (particles == null || WAVESView.BeyondCamera(position, cullingDistance))
                return;

            int id = particles.GetInstanceID();
            if (!_particles.TryGetValue(id, out ParticleVoice voice) || voice.Root == null)
            {
                voice = CreateParticles(particles);
                _particles[id] = voice;
            }

            voice.Root.SetPositionAndRotation(position, rotation);
            ParticleSystem[] systems = voice.Systems;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                    continue;

                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                system.Play(false);
            }
        }

        /// <inheritdoc />
        public void PlayGraph(
            int emitterId,
            ActorProfile actor,
            Vector3 position,
            Quaternion rotation,
            float cullingDistance,
            VisualEffectAsset graph)
        {
            if (graph == null || WAVESView.BeyondCamera(position, cullingDistance))
                return;

            int id = graph.GetInstanceID();
            if (!_graphs.TryGetValue(id, out GraphVoice voice) || voice.Effect == null)
            {
                voice = CreateGraph(graph);
                _graphs[id] = voice;
            }

            if (voice.Attribute == null)
                return;

            voice.Attribute.SetVector3(PositionId, position);
            voice.Attribute.SetVector3(AngleId, rotation.eulerAngles);
            voice.Effect.SendEvent(PlayEventId, voice.Attribute);
        }

        void OnDisable()
        {
            foreach (KeyValuePair<int, ParticleVoice> pair in _particles)
            {
                if (pair.Value.Root != null)
                    Destroy(pair.Value.Root.gameObject);
            }

            foreach (KeyValuePair<int, GraphVoice> pair in _graphs)
            {
                if (pair.Value.Effect != null)
                    Destroy(pair.Value.Effect.gameObject);
            }

            _particles.Clear();
            _graphs.Clear();
        }

        ParticleVoice CreateParticles(ParticleSystem prefab)
        {
            GameObject instance = Instantiate(prefab.gameObject, transform);
            instance.SetActive(true);
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            return new ParticleVoice
            {
                Root = instance.transform,
                Systems = systems
            };
        }

        GraphVoice CreateGraph(VisualEffectAsset asset)
        {
            var go = new GameObject(asset != null ? asset.name : "WAVES Graph");
            go.transform.SetParent(transform, false);
            VisualEffect effect = go.AddComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            effect.initialEventName = string.Empty;
            return new GraphVoice
            {
                Effect = effect,
                Attribute = effect.CreateVFXEventAttribute()
            };
        }
    }
}
