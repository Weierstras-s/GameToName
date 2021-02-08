using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

class Settings : MonoBehaviour {
    public static LayerMask playerMask = 1 << 8;
    public static LayerMask groundMask = 1 << 9;
    public static LayerMask platformMask = 1 << 10;
    public static LayerMask ladderMask = 1 << 11;
}

