using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CameraControl : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsOwner) return;
        transform.Find("Camera").gameObject.SetActive(false);
    }
}
