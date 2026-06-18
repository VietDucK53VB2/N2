# Safe Deploy Checklist

Use this checklist before updating the VPS.

## Required steps

1. Review the scope.
   - Confirm only the intended files are changing.
   - Do not touch databases or files from other groups.

2. Build locally.
   - Run the relevant frontend/backend build.
   - Verify the app still loads locally.

3. Create a clear Git checkpoint.
   - Commit the change on a feature branch.
   - Push the branch to GitHub.
   - Create a tag for rollback.

4. Verify the change on the VPS.
   - Deploy only the built artifacts or the exact commit you pushed.
   - Delete the target UI folder first, then copy the new build in one shot.
   - Never overlay-copy a new bundle on top of an old bundle.
   - Confirm the live UI still works.

5. Keep rollback simple.
   - Always leave a tag or commit hash that can be restored quickly.

## Current rollback tag

- `reader-categories-sync-20260617`

## Notes

- If the repo already contains the change on `master`, do not create a duplicate PR just for the sake of it.
- If a future task needs a VPS update, read this file first and follow the steps in order.
- For the librarian UI, the deploy is only valid when `backend/wwwroot/ui/librarian/index.html`
  points to the single current `index-*.js` and `index-*.css` entry bundles in the same folder.
