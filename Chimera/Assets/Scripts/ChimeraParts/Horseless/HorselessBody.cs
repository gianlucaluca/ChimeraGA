using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class HorselessBody : Body
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Initialize(){
        index = 4;
        health = 7;
        base.Initialize();
    }
    protected override void Update(){
        
    }
}
