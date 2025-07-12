using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class BrickStudManager
{
    private readonly BrickBehavior brick;
    private readonly List<Stud> topStuds = new List<Stud>();
    private readonly List<Stud> bottomStuds = new List<Stud>();

    public List<Stud> TopStuds => topStuds;
    public List<Stud> BottomStuds => bottomStuds;

    public BrickStudManager(BrickBehavior brick)
    {
        this.brick = brick;
        DiscoverStuds();
    }

    public void DiscoverStuds()
    {
        Debug.Log($"[{brick.name}] DiscoverStuds() - Starting stud discovery");
        
        topStuds.Clear();
        bottomStuds.Clear();

        Stud[] allStuds = brick.GetComponentsInChildren<Stud>();
        Debug.Log($"[{brick.name}] DiscoverStuds() - Found {allStuds.Length} total studs");

        foreach (Stud stud in allStuds)
        {
            // Give each stud a reference back to this parent brick
            stud.ParentBrick = brick;
            Debug.Log($"[{brick.name}] DiscoverStuds() - Set parent brick for stud: {stud.name}");

            if (stud.Type == Stud.StudType.Top)
            {
                topStuds.Add(stud);
                Debug.Log($"[{brick.name}] DiscoverStuds() - Added top stud: '{stud.name}' at local position {stud.transform.localPosition}");
            }
            else
            {
                bottomStuds.Add(stud);
                Debug.Log($"[{brick.name}] DiscoverStuds() - Added bottom stud: '{stud.name}' at local position {stud.transform.localPosition}");
            }
        }

        if (allStuds.Length == 0)
        {
            Debug.LogWarning($"[{brick.name}] DiscoverStuds() - WARNING: Brick has no 'Stud' components on its children. It won't be able to snap.");
        }
        
        Debug.Log($"[{brick.name}] DiscoverStuds() - Discovery complete. Top Studs: {topStuds.Count}, Bottom Studs: {bottomStuds.Count}");
    }

    public void DisableStudCollisions()
    {
        Debug.Log($"[{brick.name}] DisableStudCollisions() - Disabling collision detection on all studs");
        
        // Disable colliders on all studs temporarily
        foreach (var stud in topStuds)
        {
            if (stud.GetComponent<Collider>() != null)
            {
                stud.GetComponent<Collider>().enabled = false;
            }
        }
        
        foreach (var stud in bottomStuds)
        {
            if (stud.GetComponent<Collider>() != null)
            {
                stud.GetComponent<Collider>().enabled = false;
            }
        }
        
        // Re-enable after a short delay
        brick.StartCoroutine(ReenableStudCollisions());
    }
    
    // Coroutine to re-enable stud collision detection
    private IEnumerator ReenableStudCollisions()
    {
        // Wait for the snap animation to complete
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"[{brick.name}] ReenableStudCollisions() - Re-enabling collision detection on all studs");
        
        // Re-enable colliders on all studs
        foreach (var stud in topStuds)
        {
            if (stud.GetComponent<Collider>() != null)
            {
                stud.GetComponent<Collider>().enabled = true;
            }
        }
        
        foreach (var stud in bottomStuds)
        {
            if (stud.GetComponent<Collider>() != null)
            {
                stud.GetComponent<Collider>().enabled = true;
            }
        }
    }

    public void EnableStudCollisions()
    {
        Debug.Log($"[{brick.name}] EnableStudCollisions() - Enabling collision detection on all studs");
        
        // Enable colliders on all studs
        foreach (var stud in topStuds)
        {
            if (stud.GetComponent<Collider>() != null)
            {
                stud.GetComponent<Collider>().enabled = true;
            }
        }
        
        foreach (var stud in bottomStuds)
        {
            if (stud.GetComponent<Collider>() != null)
            {
                stud.GetComponent<Collider>().enabled = true;
            }
        }
    }

    public void Cleanup()
    {
        // Clear references
        topStuds.Clear();
        bottomStuds.Clear();
    }
} 