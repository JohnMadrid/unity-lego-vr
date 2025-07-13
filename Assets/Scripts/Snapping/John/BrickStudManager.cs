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
        brick.LogDebug($" DiscoverStuds() - Starting stud discovery");
        
        topStuds.Clear();
        bottomStuds.Clear();

        Stud[] allStuds = brick.GetComponentsInChildren<Stud>();
        brick.LogDebug($" DiscoverStuds() - Found {allStuds.Length} total studs");

        foreach (Stud stud in allStuds)
        {
            // Give each stud a reference back to this parent brick
            stud.ParentBrick = brick;
            brick.LogDebug($" DiscoverStuds() - Set parent brick for stud: {stud.name}");

            if (stud.Type == Stud.StudType.Top)
            {
                topStuds.Add(stud);
                brick.LogDebug($" DiscoverStuds() - Added top stud: '{stud.name}' at local position {stud.transform.localPosition}");
            }
            else
            {
                bottomStuds.Add(stud);
                brick.LogDebug($" DiscoverStuds() - Added bottom stud: '{stud.name}' at local position {stud.transform.localPosition}");
            }
        }

        if (allStuds.Length == 0)
        {
            brick.LogWarning($"[{brick.name}] DiscoverStuds() - WARNING: Brick has no 'Stud' components on its children. It won't be able to snap.");
        }
        
        brick.LogDebug($" DiscoverStuds() - Discovery complete. Top Studs: {topStuds.Count}, Bottom Studs: {bottomStuds.Count}");
    }

    public void DisableStudCollisions()
    {
        brick.LogDebug($" DisableStudCollisions() - Disabling collision detection on all studs");
        
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
        brick.LogDebug($" ReenableStudCollisions() - Waiting for snap animation to complete");
        
        // Wait for the snap animation to actually complete, not just a fixed time
        // This prevents physics interference during the lerp movement
        int waitCount = 0;
        const int MAX_WAIT_FRAMES = 120; // 2 seconds at 60fps (increased from 0.5s)
        
        while (brick.isSnapping && waitCount < MAX_WAIT_FRAMES)
        {
            waitCount++;
            if (waitCount % 30 == 0) // Log every 30 frames (about 0.5 seconds at 60fps)
            {
                brick.LogDebug($" ReenableStudCollisions() - Still waiting for snap to complete, frame {waitCount}, isSnapping: {brick.isSnapping}", true);
            }
            yield return null;
        }
        
        if (waitCount >= MAX_WAIT_FRAMES)
        {
            brick.LogWarning($" ReenableStudCollisions() - WARNING: Timeout reached! Force re-enabling collisions after {waitCount} frames");
        }
        
        brick.LogDebug($" ReenableStudCollisions() - Re-enabling collision detection on all studs after {waitCount} frames");
        
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
        brick.LogDebug($" EnableStudCollisions() - Enabling collision detection on all studs");
        
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
