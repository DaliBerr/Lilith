using System.Collections.Generic;
using UnityEngine;

namespace Kernel.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class BookNarrativeTrigger : MonoBehaviour
    {
        private readonly HashSet<Transform> activePlayerRoots = new();

        private void Reset()
        {
            EnsureTriggerCollider();
        }

        private void Awake()
        {
            EnsureTriggerCollider();
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHandlePlayerEnter(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision != null)
            {
                TryHandlePlayerEnter(collision.collider);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            TryHandlePlayerExit(other);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision != null)
            {
                TryHandlePlayerExit(collision.collider);
            }
        }

        private void OnDisable()
        {
            UIInputRouter router = UIInputRouter.Instance;
            if (router != null)
            {
                foreach (Transform playerRoot in activePlayerRoots)
                {
                    if (playerRoot != null)
                    {
                        router.UnregisterNarrativeReaderInteractor(playerRoot);
                    }
                }
            }

            activePlayerRoots.Clear();
        }

        private void TryHandlePlayerEnter(Collider other)
        {
            if (!TryResolvePlayerRoot(other, out Transform playerRoot))
            {
                return;
            }

            if (!activePlayerRoots.Add(playerRoot))
            {
                return;
            }

            UIInputRouter.Instance?.RegisterNarrativeReaderInteractor(playerRoot);
        }

        private void TryHandlePlayerExit(Collider other)
        {
            if (!TryResolvePlayerRoot(other, out Transform playerRoot))
            {
                return;
            }

            if (!activePlayerRoots.Remove(playerRoot))
            {
                return;
            }

            UIInputRouter.Instance?.UnregisterNarrativeReaderInteractor(playerRoot);
        }

        private void EnsureTriggerCollider()
        {
            Collider[] colliders = GetComponents<Collider>();
            if (colliders == null || colliders.Length == 0)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                {
                    return;
                }
            }

            if (colliders[0] != null)
            {
                colliders[0].isTrigger = true;
            }
        }

        private static bool TryResolvePlayerRoot(Collider other, out Transform playerRoot)
        {
            playerRoot = null;
            if (other == null)
            {
                return false;
            }

            PlayerPlaneMovement playerMovement = other.GetComponentInParent<PlayerPlaneMovement>();
            if (playerMovement == null)
            {
                return false;
            }

            playerRoot = playerMovement.transform.root;
            return playerRoot != null;
        }
    }
}
