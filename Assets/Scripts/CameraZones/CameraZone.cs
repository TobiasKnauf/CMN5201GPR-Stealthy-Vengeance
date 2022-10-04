using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class CameraZone : MonoBehaviour
{
    [SerializeField] private Room Room;

    private bool isActive;
    public bool IsActive
    {
        get { return isActive; }
        set
        {
            if (value == isActive) return;

            isActive = value;

            if (isActive)
            {
                Room.SpawnAllEnemies();
            }
            else
            {
                Room.DestroyAllEnemies();
            }
        }
    }
    [SerializeField] public LayerMask playerLayer;
    [HideInInspector] public Collider col;
    [SerializeField] public float cameraOrthographicSize = 11.25f;

    private void Awake()
    {
        col = GetComponent<Collider>();
        CheckForPlayer();
    }

    private void Update()
    {
        CheckForPlayer();
    }

    /// <summary>
    /// Checks if the player is within the bounds of the zone.
    /// </summary>
    private void CheckForPlayer()
    {
        Collider2D[] cols = Physics2D.OverlapBoxAll(transform.position, transform.localScale, 0f, playerLayer);
        if (cols.Length != 0)
        {
            IsActive = true;
        }
        else
        {
            IsActive = false;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, transform.localScale); //visualize zone bounds

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, new Vector2(cameraOrthographicSize * 2 * Camera.main.aspect, cameraOrthographicSize * 2));
    }
}
