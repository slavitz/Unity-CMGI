using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fps : MonoBehaviour
{
    UnityEngine.UI.Text text;

    const int LENGTH = 10;
    float[] frames = new float[LENGTH];
    int frameID = 0;

    private void Start()
    {
        text = gameObject.GetComponent<UnityEngine.UI.Text>();
    }

    // Update is called once per frame
    void Update()
    {
        frames[frameID] = 1f / Time.deltaTime;
        frameID++;
        frameID %= LENGTH;

        float total = 0;
        for (int i = 0; i < LENGTH; i++)
            total += frames[i];

        total /= (float) LENGTH;

        text.text = total + "";
    }
}
