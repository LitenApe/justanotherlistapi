'use server';

export default async function Home() {
  const { title, description } = hero[Math.floor(Math.random() * hero.length)];

  return (
    <>
      <section>
        <h1>{title}</h1>
        <p>{description}</p>
      </section>
      <section>
        <h2>Features</h2>
        <ul>
          <li>Make unlimited lists</li>
          <li>
            Share with family, friends, or that one roommate who only buy snacks
          </li>
          <li>Real-time syncing</li>
          <li>Zero fluff. Just lists</li>
        </ul>
      </section>
    </>
  );
}

const hero = [
  {
    title: 'No AI. No Blockchain. Just a List.',
    description:
      "A simple, shared shopping list. Like it's 2025 and people still forget bread",
  },
  {
    title: 'Make Lists for Things you can Actually Finish',
    description: 'Like "Buy Milk." Not "Figure out life."',
  },
  {
    title: 'Make a list So you can Ignore It Later in Style',
    description: "Or actually use it. We're not your mom",
  },
  {
    title: 'Forgetful? Same.',
    description:
      "That's why we made an app that remembers what you were supposed to",
  },
];
