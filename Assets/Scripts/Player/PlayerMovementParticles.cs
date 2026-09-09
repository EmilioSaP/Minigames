using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PlayerMovementParticles : NetworkBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;

    [SerializeField] private float walkParticleSpeed = 4f;
    [SerializeField] private float sprintParticleSpeed = 6f;

    private ParticleSystem movementParticles;
    private ParticleSystem.MainModule particleMainModule;

    private void Awake()
    {
        movementParticles = GetComponent<ParticleSystem>();
        particleMainModule = movementParticles.main;
    }

    private void Update()
    {
        // Only run this logic for the local player's input
        if (!IsOwner || playerMovement == null) return;

        if (playerMovement.IsMoving && !playerMovement.IsJumping)
        {
            // Set speed based on whether the player is sprinting or walking
            particleMainModule.startSpeed = playerMovement.IsSprinting ? sprintParticleSpeed : walkParticleSpeed;

            if (!movementParticles.isPlaying)
            {
                movementParticles.Play();
            }
        }
        else
        {
            if (movementParticles.isPlaying)
            {
                movementParticles.Stop();
            }
        }
    }
}