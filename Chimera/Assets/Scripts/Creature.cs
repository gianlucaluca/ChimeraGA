using UnityEngine;
using System.Collections.Generic;
using System;

public abstract class Creature : MonoBehaviour, Entity
{
    protected bool hostile;
    [SerializeField] GameObject Chimerafab;
    protected int health = 10;
    protected int maxHealth = 10;
    protected int attack = 1;
    protected float attackSpeed = 0.7f; //attack speed in time between attacks
    //[SerializeField] protected Collider2D collisions;
    protected Rigidbody2D rgb;
    [SerializeField] protected Collider2D trig;
    protected Creature aggro;
    protected float clock = 0;
    protected List<Creature> inTrigger;
    [SerializeField] protected int attackRange = 15;
    [SerializeField] protected int speed = 300;
    int attackCount = 0;
    protected Head head;
    protected Body body;
    protected Tail tail;
    public event Action<float> OnHealthChanged = delegate { };
    // keeps track of creatures that cannot be aggroed. Mainly for Eye Candy's attract ability: once it ends, the eye candy will be added to this temporarily to allow the eye candy to escape
    private readonly List<Creature> disabledAggroTargets = new();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected void Start()
    {
        head = gameObject.GetComponentInChildren<Head>();
        body = gameObject.GetComponentInChildren<Body>();
        tail = gameObject.GetComponentInChildren<Tail>();
        trig = gameObject.GetComponent<Collider2D>();
        health = body.getHealth();
        maxHealth = body.getHealth();
        attack = tail.getAttack();
        rgb = GetComponent<Rigidbody2D>();
        inTrigger = new List<Creature>();
        head.GetComponent<Animator>().SetBool("IsChimera", !hostile);
        body.GetComponent<Animator>().SetBool("IsChimera", !hostile);
        tail.GetComponent<Animator>().SetBool("IsChimera", !hostile);

        // event responses
        EyeCandyHead.onEyeCandyTriggerAggro.AddListener(OnEyeCandyTriggerAggroResponse);
        EyeCandyHead.onEyeCandyTriggerDisableAggro.AddListener(OnEyeCandyTriggerDisableAggroResponse);
        EyeCandyHead.onEyeCandyTriggerReenableAggro.AddListener(OnEyeCandyTriggerReenableAggroResponse);
        clock = Time.time;
    }

    // Update is called once per frame
    protected void Update()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 30f);
        foreach (Collider col in colliders)
        {
            Debug.Log("Overlap detected with: " + col.gameObject.name);
        }
        if (aggro != null)
        {
            //head towards object of aggro
            Vector2 aggPos = new Vector2(aggro.gameObject.GetComponent<Transform>().position.x, aggro.gameObject.GetComponent<Transform>().position.y);
            Vector2 pos = (Vector2)transform.position;
            if (Vector2.Distance(aggPos, pos) > attackRange)
            {
                Vector2 newPos = Vector2.MoveTowards(transform.position, aggPos, speed * Time.deltaTime);
                rgb.MovePosition(newPos);
            }
            else
            {
                if (Time.time - clock > attackSpeed)
                {
                    clock = Time.time;
                    attackCount++;
                    Attack(aggro); //every second, while aggro is within attack range, attack aggro target
                }
            }
        } else
        {
            rgb.linearVelocity = Vector2.zero;
        }
    }

    public bool takeDamage(int dmg)
    {
        Debug.Log(hostile ? "Enemy took damage" : "Ally took damage");
        dmg = body.takeDamage(dmg);
        health -= dmg;
        float healthPercent = (float)health / (float)maxHealth;
        OnHealthChanged(healthPercent);
        if (health <= 0)
        {
            Die();
            return true;
        }
        return false;
    }

    public void Attack(Creature target)
    {
        Debug.Log(hostile ? "Enemy attacked" : "Ally attacked");
        bool died = tail.Attack(target);
        if (died)
        {
            aggro = null;
            reAggro();
            if (this.hostile == false)
            {
                Globals.energy += 5;
            }
        }
    }

    public void Die()
    {
        if (this.hostile == false)
        {
            //find this creature in inventory and remove them
            NewChimeraStats thisChimera = new NewChimeraStats(this.head.gameObject, this.body.gameObject, this.tail.gameObject, Chimerafab);
            ChimeraParty.RemoveChimera(thisChimera);
        }
        head.GetComponent<Animator>().SetBool("IsAlive", false);
        body.GetComponent<Animator>().SetBool("IsAlive", false);
        tail.GetComponent<Animator>().SetBool("IsAlive", false);
        Destroy(this.gameObject);
    }

    protected void OnTriggerEnter2D(Collider2D other)
    {
        //Debug.Log("New trigger enter");
        if (aggro == null && (other.gameObject.GetComponent<Creature>() != null))
        {
            if (other.gameObject.GetComponent<Creature>().hostile != hostile)
            {
                aggro = other.gameObject.GetComponent<Creature>(); //only aggro if it's an enemy Creature
                Debug.Log("Aggroed");
            }
        }
        else if (other.gameObject.GetComponent<Creature>() != null && other.gameObject.GetComponent<Creature>().hostile != hostile)
        {
            inTrigger.Add(other.gameObject.GetComponent<Creature>());
        }
    }
    protected void OnCollisionEnter2D(Collision2D other)
    {
        Debug.Log("Collision");
    }
    //so right now, the first enemy to enter trigger is aggro'd onto until it dies or leaves the trigger (when eyeball moves)
    protected void OnTriggerExit2D(Collider2D other)
    {
        //Debug.Log("Trigger Exit");
        if (other != null && aggro != null)
        {
            if (other.gameObject == aggro.gameObject)
            {
                aggro = null; //if currently aggro'd object leaves trigger colllider, stops aggroing it
                reAggro();
            }
            else if (other.gameObject.GetComponent<Creature>() != null)
            {
                inTrigger.Remove(other.gameObject.GetComponent<Creature>());
            }
        }
    }

    protected void reAggro()
    {
        Debug.Log("New aggro");
        if (inTrigger.Count > 0)
        {
            // if the first inTrigger element is disabled, search recursively for an enabled element
            if (disabledAggroTargets.Contains(inTrigger[0]))
            {
                ReAggro(1);
                return;
            }

            // default
            aggro = inTrigger[0]; //take off a Creature in the collider, make that new aggro target
            inTrigger.RemoveAt(0);
        }
        else aggro = null;
    }

    // A recursive overload for use to select the first non-disabled member of inTrigger while not forgetting the other members: like normal reAggro, but checks at index numFrontDisabled instead of index 0
    protected void ReAggro(int numFrontDisabled)
    {
        if (inTrigger.Count > numFrontDisabled)
        {
            // for disabled aggros
            if (disabledAggroTargets.Contains(inTrigger[numFrontDisabled]))
            {
                ReAggro(numFrontDisabled + 1);
                return;
            }

            // default
            aggro = inTrigger[numFrontDisabled]; //take off a Creature in the collider, make that new aggro target
            inTrigger.RemoveAt(numFrontDisabled);
        }
        else aggro = null;
    }

    // Event callback for Eye Candy's distract ability start of distract period: adds the Eye Candy head's chimera to the front of this creature's inTrigger
    protected void OnEyeCandyTriggerAggroResponse(Creature eyeCandy, double distractRadius)
    {
        // Debug.Log($"{this} heard Eye Candy's ability start distract period"); 

        // guard clause: on opposing teams
        if (this.hostile == eyeCandy.IsHostile())
        {
            // Debug.Log($"{this} is on the same side as Eye Candy");
            return;
        }

        // guard clause: close enough
        if (DistanceTo(eyeCandy) > distractRadius)
        {
            // Debug.Log($"{this} is too far away from Eye Candy");
            return;
        }

        // distract
        // Debug.Log($"{this} was distracted by Eye Candy");
        inTrigger.Insert(0, eyeCandy);
        reAggro();
    }

    // Event callback for Eye Candy's distract ability start of escape period: removes the Eye Candy head's chimera from the front of this creature's inTrigger if present
    protected void OnEyeCandyTriggerDisableAggroResponse(Creature eyeCandy)
    {
        // Debug.Log($"{this} heard Eye Candy's ability start escape period");

        // if aggroing Eye Candy, stop, disable Eye Candy aggroing, and pick a new target
        if (inTrigger.Count > 0 && inTrigger[0] == eyeCandy)
        {
            // Debug.Log($"{this} lost interest in Eye Candy");
            inTrigger.RemoveAt(0);
            SetAggroable(eyeCandy, false);
            reAggro();
        }
    }

    // Event callback for Eye Candy's distract ability end: removes eyeCandy from current
    protected void OnEyeCandyTriggerReenableAggroResponse(Creature eyeCandy)
    {
        SetAggroable(eyeCandy, true);
        // Debug.Log($"{this} heard Eye Candy's ability end escape period");
    }

    // helper function: adds/removes given creature from list of unaggroable creatures based on bool false/true, respectively
    private void SetAggroable(Creature c, bool b)
    {
        if (b)
        {
            // Debug.Log("Set aggroable");
            for (int i = 0; i < disabledAggroTargets.Count; i++)
            {
                if (disabledAggroTargets[i] == c)
                {
                    disabledAggroTargets.RemoveAt(i);
                    i--;
                }
            }
        }
        else
        {
            // Debug.Log("Set unaggroable");
            disabledAggroTargets.Add(c);
        }
    }   

    // getter for hostile
    public bool IsHostile()
    {
        return hostile;
    }

    // helper function: distance from this to a game obj
    private double DistanceTo(MonoBehaviour mb)
    {
        return (mb.GetComponent<Transform>().position - transform.position).magnitude;
    }

    public override string ToString()
    {
        return "Creature {Head: " + head.name + ", Body: " + body.name + ", Tail: " + tail.name + ", isHostile: " + hostile + "}"; 
    }
}
