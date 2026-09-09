# College roles and martial skills - conversation notes

Reference conversation supplied by the user: Add College Roles Fork.html.
This is a summary of decisions, not a verbatim transcript.

- Ask for confirmation before editing base SS14 files. Imperial/Medieval-specific implementation is authorized.
- College Soldier and Arcane Knight have separate grimoires and fixed placeholder balances of 6 Skill Points and 6 Magic Talent.
- Both use Physical Skills and Magic Skills categories. Learning resources do not regenerate.
- Skill prototypes now live under Resources/Prototypes/Imperial/Medieval/Skills/MartialSkills.
- NoviceDashSkill (Short Dash) has a 10-second action cooldown and a 10-stamina base cost.
- The skill is independent of MedievalDash: MartialDashEvent and MedievalPhysicalSystem handle it. OnSkillResource configures its stamina cost.
- Four-tile maximum targeting distance, speed 12, and the Blink icon are placeholders. Movement uses normal physics collision and a wall ray check.
- Dash store.yml and effect.yml remain empty. A learning cost has not been selected.
- The supplied HTML records the user's current intent and plans; newer direct instructions take precedence.
- Follow Option B from the HTML: explicitly declare Action, TargetAction, and WorldTargetAction, without inheriting BaseAction.
- Use MedievalPhysicalSystem for physical skills. Keep the independent MartialDashEvent name to avoid coupling to the existing MedievalDashEvent.
- Do not add CastTraining, ManaDrainSpell, MedievalTargetSpell, teleport effects, ActionUpgrade, or ShowSpawnedEntity for the initial Dash.
