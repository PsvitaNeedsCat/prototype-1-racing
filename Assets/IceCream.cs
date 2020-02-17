﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IceCream : MonoBehaviour
{
    public float newDrag = 2;
    private float oldDrag = -1.0f;

    private void OnTriggerEnter(Collider other)
    {
        
        Player player = other.GetComponent<Player>();

        if (player)
        {
            Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();
            if (oldDrag < 0.0f) { oldDrag = playerRigidbody.drag; }

            playerRigidbody.drag = newDrag;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Player player = other.GetComponent<Player>();

        if (player)
        {
            Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();
            playerRigidbody.drag = oldDrag;
        }
    }
}