﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Defines the player's movements mechanics, including character switching, jumping, wall jumping, and all the button input reading necessary.
/// </summary>
public class PlayerController : AbstractController
{
    // State info 
    private bool isCrowActive; // true for crow, false for cat
    private bool isDoublejumpAvailable;
    public bool IsDoublejumpAvailable { get { return isDoublejumpAvailable; } set { isDoublejumpAvailable = value; } }
    private Collision2D enemyCollision;
    public bool CollidedWithEnemy { get { return enemyCollision != null; } set { if (!value) { enemyCollision = null; } } }
    public Collision2D EnemyCollision { get { return enemyCollision; } }
    [SerializeField] private int healthPoints;
    [SerializeField] private GameObject[] objectives;
    [SerializeField] private int obtainedObjectives;
    private bool invincible;
    [SerializeField] private int invincibilityDuration;
    [SerializeField] private float blinkDuration;

    // Input manager to be able to change keys during runtime
    private KeyBinding keyBinding;

    // The controls inputted by the player
    protected bool jump;
    public bool Jump { get { return jump; } set { jump = value; } }
    protected float horizontalMovement;
    public float HorizontalMovement { get { return horizontalMovement; } }
    private bool switchAnimal;

    // State variables to handle wall jump hitstun
    private bool wallHitstunDirection; // true for right, false for left
    public bool WallHitstunDirection { get { return wallHitstunDirection; } set { wallHitstunDirection = value; } }
    private int wallHitstunCounter = 0;
    public int WallHitstunCounter { get { return wallHitstunCounter; } set { wallHitstunCounter = value; } }

    // State variables to handle enemy hitstun
    private bool enemyHitstunDirection; // true for right, false for left
    public bool EnemyHitstunDirection { get { return enemyHitstunDirection; } set { enemyHitstunDirection = value; } }
    private int enemyHitstunCounter = 0;
    public int EnemyHitstunCounter { get { return enemyHitstunCounter; } set { enemyHitstunCounter = value; } }

    // Enemy knockback tweaks
    [SerializeField] private float enemyKnockbackUpwardsForce;
    public float EnemyKnockbackUpwardsForce { get { return enemyKnockbackUpwardsForce; } }
    [SerializeField] private float enemyKnockbackSidewaysForce;
    public float EnemyKnockbackSidewaysForce { get { return enemyKnockbackSidewaysForce; } }
    [SerializeField] private int enemyHitstunDuration;
    public int EnemyHitstunDuration { get { return enemyHitstunDuration; } }
    [SerializeField] private float moveInfluenceAfterEnemyKnockback;
    public float MoveInfluenceAfterEnemyKnockback { get { return moveInfluenceAfterEnemyKnockback; } }

    // Wall jumping tweaks
    [SerializeField] private float wallJumpUpwardsForce;
    public float WallJumpUpwardsForce { get { return wallJumpUpwardsForce; } }
    [SerializeField] private float wallJumpSidewaysForce;
    public float WallJumpSidewaysForce { get { return wallJumpSidewaysForce; } }
    [SerializeField] private int walljumpMovementDuration;
    public int WalljumpMovementDuration { get { return walljumpMovementDuration; } }
    [SerializeField] private float moveInfluenceAfterWalljump;
    public float MoveInfluenceAfterWalljump { get { return moveInfluenceAfterWalljump; } }

    // Cache Unity objects that are used frequently to avoid getting them every time
    protected Animator anneAnimator;
    protected GameObject clemmObject;
    protected GameObject ultharObject;
    protected Animator currentAnimator;
    protected Collider2D characterCollider;
    public Collider2D CharacterCollider { get { return characterCollider; } }
    [SerializeField] protected TextMeshProUGUI healthText;
    [SerializeField] protected TextMeshProUGUI objectivesText;
    [SerializeField] protected Transform spawnPoint;
    private Renderer characterRenderer;

    // Start is called before the first frame update.
    public override void Start()
    {
        // Call parent to initialize all the necessary stuff
        base.Start();

        // Unity stuff
        characterCollider = GetComponent<Collider2D>();
        anneAnimator = GetComponent<Animator>();
        ultharObject = transform.Find("Ulthar").gameObject;
        clemmObject = transform.Find("Clemm").gameObject;
        characterRenderer = GetComponent<Renderer>();

        keyBinding = KeyBinding.Instance;
        
        // Clemm is the default animal
        isCrowActive = true;
        ultharObject.SetActive(false);
        currentAnimator = clemmObject.GetComponent<Animator>();

        // Custom stuff
        movementStrategy = new PlayerCrowMovementStrategy(this); // Default animal is crow
        UpdateHealthText();
        UpdateObjectivesText();

        // Setup for invincibility frames
        invincible = false;
    }

    // Update is called once per frame
    void Update()
	{
        // Read player input each update 
        ReadPlayerInput();
    }

    /// <summary>
    /// Reads player input and sets the appropriate info for the movement startegy to use.
    /// </summary>
    private void ReadPlayerInput()
    {
        // Determine horizontal movement

        // First we attempt to use Input.GetAxisRaw in case the player is using a controller.
        // We use GetAxisRaw to avoid Unity's automatic smoothing to enable the player to stop on a dime.
        // Multiply the input by our movement speed to allow controller users to input analog movement 
        horizontalMovement = Input.GetAxisRaw("Horizontal") * movementSpeed;

        // If no axis movement was detected, read the keys with out custom keybinding
        if(horizontalMovement == 0)
        {
            if(keyBinding.GetKey("Left") && !keyBinding.GetKey("Right"))
            {
                horizontalMovement = -movementSpeed;
            } else if (!keyBinding.GetKey("Left") && keyBinding.GetKey("Right"))
            {
                horizontalMovement = movementSpeed;
            }
        }

        // Determine if player wants to jump
        // We only want to change jump if it is already false, changing its value when its true can result in missed inputs
        if (!jump)
        {
            jump = keyBinding.GetKeyDown("Jump");
        }

        // Check if the current animal should be switched, using the same method as with jumps
        if (!switchAnimal)
        {
            switchAnimal = keyBinding.GetKeyDown("Switch");
        }
        
    }

    /// <summary>
    /// Changes the current animal, which involves three operations:
    /// 1. Change the player's sprite.
    /// 2. Change the movement strategy.
    /// 3. Flip the crowIsActive boolean.
    /// </summary>
    void SwitchAnimal()
    {
        // Change movement strategy and set the current animator, according to the currently active animal.
        if (isCrowActive)
        {
            movementStrategy = new PlayerCatMovementStrategy(this);
            currentAnimator = ultharObject.GetComponent<Animator>();
            //Debug.Log("Cat selected");
        } else
        {
            movementStrategy = new PlayerCrowMovementStrategy(this);
            currentAnimator = clemmObject.GetComponent<Animator>();
            //Debug.Log("Crow selected");
        }

        // Change the active game object and current animator
        ultharObject.SetActive(isCrowActive);
        clemmObject.SetActive(!isCrowActive);

        // Flip the currently active animal boolean.
        isCrowActive = !isCrowActive;
    }

    /// <summary>
    /// Handle animal switching and the setting of animation parameters in FixedUpdate().
    /// </summary>
    protected override void AdditionalFixedUpdateOperations()
    {
        if (switchAnimal)
        {
            SwitchAnimal();
            switchAnimal = false;
        }

        // Set the parameters for our animation
        anneAnimator.SetFloat("Speed", Mathf.Abs(rigidBody.velocity.x));
        currentAnimator.SetFloat("Speed", Mathf.Abs(rigidBody.velocity.x));
        currentAnimator.SetBool("Jump", Jump);
    }

    /// <summary>
    /// We override the abstract controller's way of handling a ground collision so we can refund the player's double jump.
    /// </summary>
    /// <param name="collision"></param>
    protected override void OnGroundCollisionEnter(Collision2D collision)
    {
        base.OnGroundCollisionEnter(collision);
        //Debug.Log("just got grounded on enter");
        isDoublejumpAvailable = true;
    }

    /// <summary>
    /// We override the abstract controller's way of handling a ground collision so we can refund the player's double jump.
    /// </summary>
    /// <param name="collision"></param>
    protected override void OnGroundCollisionStay(Collision2D collision)
    {
        base.OnGroundCollisionEnter(collision);
        //Debug.Log("just got grounded on stay");
        isDoublejumpAvailable = true;
    }


    /// <summary>
    /// Take damage and move the player away from the enemy.
    /// </summary>
    protected override void OnEnemyCollisionEnter(Collision2D collision)
    {
        if (!invincible)
        {
            isDoublejumpAvailable = true;
            healthPoints = healthPoints - 1;
            if (healthPoints > 0)
            {
                UpdateHealthText();
                enemyCollision = collision;
                StartCoroutine(Blink());
                StartCoroutine(InvincibilityFrames());
            }
            else
            {
                Respawn();
            }
            
        }
    }

    protected IEnumerator InvincibilityFrames()
    {
        this.invincible = true;
        yield return new WaitForSeconds(this.invincibilityDuration);
        this.invincible = false;      
    }

    /// <summary>
    /// Makes the player blink while the invincibility frames are active
    /// </summary>
    protected IEnumerator Blink()
    {
        float endTime = Time.time + this.invincibilityDuration;

        while(Time.time < endTime)
        {
            characterRenderer.enabled = false;
            yield return new WaitForSeconds(this.blinkDuration);
            characterRenderer.enabled = true;
            yield return new WaitForSeconds(this.blinkDuration);
        }
    }

    protected override void OnFinishCollisionEnter(Collider2D collider)
    {
        SceneManager.LoadScene("MainMenu");
    }

    protected override void OnBottomlessPitCollisionEnter(Collider2D collider)
    {
        Respawn();
    }

    protected override void OnCheckpointCollisionEnter(Collider2D collider)
    {
        spawnPoint = collider.transform;
    }

    protected override void OnObjectiveCollisionEnter(Collider2D collider)
    {
        // Disable the object collided against
        collider.gameObject.SetActive(false);

        // Update objectives
        obtainedObjectives++;
        if(obtainedObjectives == objectives.Length)
        {
            // If the player has all the objectives, go to the main menu
            SceneManager.LoadScene("MainMenu");
        } else
        {
            // Otherwise, update the objectives text
            UpdateObjectivesText();
        }
    }

    /// <summary>
    /// Write the current health points on screen.
    /// </summary>
    private void UpdateHealthText() 
    {
       healthText.SetText("Health: " + healthPoints.ToString());
    }

    /// <summary>
    /// Write the current health points on screen.
    /// </summary>
    private void UpdateObjectivesText()
    {
        if(objectives!=null && objectivesText != null)
        {
            objectivesText.SetText("Objectives: " + obtainedObjectives+"/"+ objectives.Length);
        }
        
    }

    /// <summary>
    /// Move player to spawnpoint, remove their speed and restore their healthpoints.
    /// Re-enable all objectives.
    /// </summary>
    private void Respawn()
    {
        rigidBody.position = spawnPoint.position;
        rigidBody.velocity = new Vector2();
        healthPoints = 3;
        UpdateHealthText();
        obtainedObjectives = 0;
        UpdateObjectivesText();
        for (int i = 0; i < objectives.Length; i++) {
            objectives[i].SetActive(true);
        }
    }

}
