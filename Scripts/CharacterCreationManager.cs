using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public class CharacterCreationProfile
    {
        public string Name { get; set; }
        public string Nationality { get; set; }
        public string Job { get; set; }
        public string Weapon { get; set; }
        public List<string> StartingEquipment { get; set; } = new List<string>();

        public CharacterCreationProfile Clone()
        {
            return new CharacterCreationProfile
            {
                Name = Name,
                Nationality = Nationality,
                Job = Job,
                Weapon = Weapon,
                StartingEquipment = new List<string>(StartingEquipment)
            };
        }
    }

    public class CharacterCreationManager
    {
        private class JobLoadout
        {
            public string Weapon { get; set; }
            public List<string> Equipment { get; set; } = new List<string>();
        }

        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;
        private readonly Random _random;

        private readonly string[] _nationalities =
        {
            "Canadian", "American", "French", "British", "German", "Italian", "Spanish", "Japanese"
        };

        private readonly Dictionary<string, JobLoadout> _jobLoadouts = new Dictionary<string, JobLoadout>
        {
            ["Cook"] = new JobLoadout { Weapon = "Remington 870", Equipment = new List<string> { "MK 2" } },
            ["Garbage collector"] = new JobLoadout { Weapon = "PR-24 Tonfa", Equipment = new List<string> { "MK 2" } },
            ["Teacher"] = new JobLoadout { Weapon = "M16A1", Equipment = new List<string> { "MK 2" } },
            ["Nurse"] = new JobLoadout { Weapon = "Beretta 92FS", Equipment = new List<string> { "MK 2" } },
            ["Mechanic"] = new JobLoadout { Weapon = "H&K MP5", Equipment = new List<string> { "MK 2" } },
            ["Firefighter"] = new JobLoadout { Weapon = "Mossberg 590", Equipment = new List<string> { "MK 2" } }
        };

        private List<CharacterCreationProfile> _profiles = new List<CharacterCreationProfile>();
        private List<Button> _slotButtons = new List<Button>();
        private Button _nextNationalityButton;
        private Button _nextJobButton;
        private Button _randomizeButton;
        private Button _startButton;
        private Button _backButton;

        private int _selectedSlot;

        public event Action<List<CharacterCreationProfile>> OnCharacterCreationCompleted;
        public event Action OnBackToMainMenu;

        public CharacterCreationManager(SpriteBatch spriteBatch, SpriteFont font, Random random)
        {
            _spriteBatch = spriteBatch;
            _font = font;
            _random = random;
        }

        public void LoadContent()
        {
            CreateDefaultProfiles();
            CreateButtons();
            RefreshSlotButtonLabels();
        }

        public void Update(MouseState mouseState, MouseState previousMouseState)
        {
            for (int i = 0; i < _slotButtons.Count; i++)
            {
                if (_slotButtons[i].IsClicked(mouseState, previousMouseState))
                {
                    _selectedSlot = i;
                    RefreshSlotButtonLabels();
                    return;
                }
            }

            if (_nextNationalityButton.IsClicked(mouseState, previousMouseState))
            {
                CycleNationality();
                return;
            }

            if (_nextJobButton.IsClicked(mouseState, previousMouseState))
            {
                CycleJob();
                return;
            }

            if (_randomizeButton.IsClicked(mouseState, previousMouseState))
            {
                RandomizeAll();
                return;
            }

            if (_startButton.IsClicked(mouseState, previousMouseState))
            {
                OnCharacterCreationCompleted?.Invoke(_profiles.Select(p => p.Clone()).ToList());
                return;
            }

            if (_backButton.IsClicked(mouseState, previousMouseState))
            {
                OnBackToMainMenu?.Invoke();
            }
        }

        public void Draw()
        {
            MouseState mouse = Mouse.GetState();

            _spriteBatch.DrawString(_font, "Creation de personnage", new Vector2(20, 20), UIThemeManager.PrimaryColor, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, "Configure 6 recrues (nationalite, metier, equipement)", new Vector2(20, 64), UIThemeManager.SecondaryColor);

            foreach (var button in _slotButtons)
                button.Draw(_spriteBatch, _font, mouse);

            _nextNationalityButton.Draw(_spriteBatch, _font, mouse);
            _nextJobButton.Draw(_spriteBatch, _font, mouse);
            _randomizeButton.Draw(_spriteBatch, _font, mouse);
            _startButton.Draw(_spriteBatch, _font, mouse);
            _backButton.Draw(_spriteBatch, _font, mouse);

            var profile = _profiles[_selectedSlot];
            float detailsX = 420;
            float y = 130;

            _spriteBatch.DrawString(_font, $"Recrue {_selectedSlot + 1}: {profile.Name}", new Vector2(detailsX, y), UIThemeManager.PrimaryColor);
            y += 32;
            _spriteBatch.DrawString(_font, $"Nationality: {profile.Nationality}", new Vector2(detailsX, y), UIThemeManager.SecondaryColor);
            y += 28;
            _spriteBatch.DrawString(_font, $"Job: {profile.Job}", new Vector2(detailsX, y), UIThemeManager.SecondaryColor);
            y += 28;
            _spriteBatch.DrawString(_font, $"Weapon: {profile.Weapon}", new Vector2(detailsX, y), UIThemeManager.SecondaryColor);
            y += 34;
            _spriteBatch.DrawString(_font, "Starting equipment:", new Vector2(detailsX, y), UIThemeManager.PrimaryColor);
            y += 26;

            foreach (string item in profile.StartingEquipment)
            {
                _spriteBatch.DrawString(_font, $"- {item}", new Vector2(detailsX + 12, y), UIThemeManager.SecondaryColor);
                y += 24;
            }
        }

        private void CreateDefaultProfiles()
        {
            string[] names = { "Nadia", "Alex", "Maya", "Victor", "Elena", "Jonas" };
            string firstJob = _jobLoadouts.Keys.First();

            _profiles = names.Select((name, i) =>
            {
                string nationality = _nationalities[i % _nationalities.Length];
                return CreateProfile(name, nationality, firstJob);
            }).ToList();
        }

        private CharacterCreationProfile CreateProfile(string name, string nationality, string job)
        {
            JobLoadout loadout = _jobLoadouts[job];
            return new CharacterCreationProfile
            {
                Name = name,
                Nationality = nationality,
                Job = job,
                Weapon = loadout.Weapon,
                StartingEquipment = new List<string>(loadout.Equipment)
            };
        }

        private void CreateButtons()
        {
            _slotButtons = Enumerable.Range(0, 6)
                .Select(i => new Button(string.Empty, new Vector2(20, 120 + i * 34)))
                .ToList();

            _nextNationalityButton = new Button("Changer Nationality", new Vector2(420, 360));
            _nextJobButton = new Button("Changer Job", new Vector2(420, 396));
            _randomizeButton = new Button("Randomiser l'equipe", new Vector2(420, 452));
            _startButton = new Button("Valider et choisir mission", new Vector2(420, 520));
            _backButton = new Button("Retour menu", new Vector2(420, 556));
        }

        private void CycleNationality()
        {
            CharacterCreationProfile p = _profiles[_selectedSlot];
            int index = Array.IndexOf(_nationalities, p.Nationality);
            p.Nationality = _nationalities[(index + 1) % _nationalities.Length];
            RefreshSlotButtonLabels();
        }

        private void CycleJob()
        {
            CharacterCreationProfile p = _profiles[_selectedSlot];
            var jobs = _jobLoadouts.Keys.ToList();
            int index = jobs.IndexOf(p.Job);
            string nextJob = jobs[(index + 1) % jobs.Count];
            JobLoadout loadout = _jobLoadouts[nextJob];

            p.Job = nextJob;
            p.Weapon = loadout.Weapon;
            p.StartingEquipment = new List<string>(loadout.Equipment);
            RefreshSlotButtonLabels();
        }

        private void RandomizeAll()
        {
            var jobs = _jobLoadouts.Keys.ToList();

            for (int i = 0; i < _profiles.Count; i++)
            {
                CharacterCreationProfile p = _profiles[i];
                p.Nationality = _nationalities[_random.Next(_nationalities.Length)];

                string job = jobs[_random.Next(jobs.Count)];
                JobLoadout loadout = _jobLoadouts[job];
                p.Job = job;
                p.Weapon = loadout.Weapon;
                p.StartingEquipment = new List<string>(loadout.Equipment);
            }

            RefreshSlotButtonLabels();
        }

        private void RefreshSlotButtonLabels()
        {
            for (int i = 0; i < _slotButtons.Count; i++)
            {
                CharacterCreationProfile p = _profiles[i];
                string marker = i == _selectedSlot ? "[Selected]" : "";
                string separator = string.IsNullOrEmpty(marker) ? string.Empty : " ";
                _slotButtons[i].Text = $"{marker}{separator}{i + 1}. {p.Name} ({p.Nationality}, {p.Job})";
            }
        }
    }
}
