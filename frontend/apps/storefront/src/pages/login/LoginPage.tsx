import heroImage from "../../assets/banner 2.png";

const benefits = [
  { title: "Quality products", description: "Authentic workspace gear with reliable warranty coverage.", icon: "✓" },
  { title: "Fast delivery", description: "Safe nationwide shipping for your setup essentials.", icon: "→" },
  { title: "Dedicated support", description: "Workspace advice and after-sales support when you need it.", icon: "◎" }
];

const trustBadges = ["Information security", "Secure checkout", "24/7 support"];

export function LoginPage() {
  return (
    <section className="relative -mx-8 -my-8 min-h-[calc(100vh-5rem)] overflow-hidden bg-[#f7fbf9] text-slate-950">
      <div className="pointer-events-none absolute -left-28 bottom-0 h-80 w-80 rounded-full bg-emerald-100/70 blur-3xl" />
      <div className="pointer-events-none absolute right-20 top-0 h-56 w-56 rounded-full bg-slate-200/60 blur-3xl" />

      <div className="relative grid min-h-[calc(100vh-5rem)] lg:grid-cols-[1fr_minmax(520px,0.92fr)]">
        <div className="relative flex min-h-[620px] flex-col overflow-hidden px-8 py-8 sm:px-12 lg:px-16 lg:py-12">
          <div className="text-2xl font-black tracking-tight sm:text-3xl">
            Workspace<span className="text-[var(--brand)]">Ecom</span>
          </div>

          <div className="relative z-10 mt-16 max-w-xl">
            <h1 className="text-5xl font-black leading-[1.08] tracking-tight sm:text-6xl lg:text-7xl">
              Build Your Perfect <span className="relative inline-block text-[var(--brand)]">Workspace<span className="absolute -bottom-2 left-2 h-2 w-[92%] rounded-full bg-[var(--brand)]/80" /></span>
            </h1>
            <p className="mt-8 max-w-md text-lg font-semibold leading-8 text-slate-500">
              Premium furniture and accessories designed to improve focus, comfort, and everyday productivity.
            </p>
          </div>

          <div className="relative z-10 mt-10 grid max-w-md gap-5">
            {benefits.map((benefit) => (
              <div key={benefit.title} className="flex items-center gap-4">
                <div className="grid h-14 w-14 shrink-0 place-items-center rounded-full bg-emerald-100 text-xl font-black text-[var(--brand)]">
                  {benefit.icon}
                </div>
                <div>
                  <p className="font-black text-slate-950">{benefit.title}</p>
                  <p className="mt-1 text-sm font-semibold text-slate-500">{benefit.description}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="pointer-events-none absolute bottom-0 left-1/2 hidden w-[760px] -translate-x-[38%] lg:block">
            <div className="absolute -left-14 bottom-16 h-44 w-44 rounded-full bg-emerald-200/55 blur-2xl" />
            <img src={heroImage} alt="Workspace desk setup" className="relative h-[430px] w-full rounded-t-[3rem] object-cover object-center shadow-2xl shadow-slate-900/10" />
          </div>
        </div>

        <div className="flex items-center justify-center px-5 py-8 sm:px-8 lg:px-12">
          <div className="w-full max-w-[620px] rounded-[2rem] bg-white/88 p-6 shadow-2xl shadow-slate-900/10 ring-1 ring-white/80 backdrop-blur-xl sm:rounded-[2.4rem] sm:p-10">
            <div className="grid grid-cols-2 border-b border-slate-200 text-lg font-black text-slate-500">
              <button type="button" className="flex items-center justify-center gap-3 border-b-2 border-[var(--brand)] pb-5 text-[var(--brand)]">
                <span className="text-2xl">↪</span>
                Login
              </button>
              <button type="button" className="flex cursor-default items-center justify-center gap-3 pb-5" aria-disabled="true">
                <span className="text-2xl">♙</span>
                Register
              </button>
            </div>

            <form
              className="mt-10"
              onSubmit={(event) => event.preventDefault()}
              noValidate
            >
              <div>
                <h2 className="text-3xl font-black tracking-tight sm:text-4xl">Welcome back</h2>
                <p className="mt-4 text-base font-semibold text-slate-500">Login will be available when customer accounts are added after the MVP.</p>
              </div>

              <div className="mt-8 grid gap-5">
                <label className="block">
                  <span className="mb-2 block font-black text-slate-900">Email</span>
                  <span className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm transition focus-within:border-[var(--brand)] focus-within:ring-4 focus-within:ring-emerald-100">
                    <span className="text-xl text-slate-400">✉</span>
                    <input className="w-full bg-transparent text-base font-semibold outline-none placeholder:text-slate-400" type="email" placeholder="Enter your email" autoComplete="email" />
                  </span>
                </label>

                <label className="block">
                  <span className="mb-2 block font-black text-slate-900">Password</span>
                  <span className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm transition focus-within:border-[var(--brand)] focus-within:ring-4 focus-within:ring-emerald-100">
                    <span className="text-xl text-slate-400">▣</span>
                    <input className="w-full bg-transparent text-base font-semibold outline-none placeholder:text-slate-400" type="password" placeholder="Enter your password" autoComplete="current-password" />
                    <span className="text-lg text-slate-400">◉</span>
                  </span>
                </label>
              </div>

              <div className="mt-6 flex items-center justify-between gap-4 text-sm font-black">
                <label className="inline-flex items-center gap-3 text-slate-600">
                  <input type="checkbox" defaultChecked className="h-5 w-5 rounded border-slate-300 accent-[var(--brand)]" />
                  Remember me
                </label>
                <button type="button" className="cursor-default text-[var(--brand)]" aria-disabled="true">Forgot password?</button>
              </div>

              <button type="submit" className="mt-8 flex w-full items-center justify-center gap-3 rounded-xl bg-[var(--brand)] px-5 py-4 text-base font-black text-white shadow-lg shadow-emerald-600/20 transition hover:bg-emerald-700">
                Login
                <span>›</span>
              </button>

              <div className="my-8 grid grid-cols-[1fr_auto_1fr] items-center gap-4 text-sm font-bold text-slate-400">
                <span className="h-px bg-slate-200" />
                or
                <span className="h-px bg-slate-200" />
              </div>
              <p className="mt-8 text-center text-sm font-semibold text-slate-500">
                Do not have an account? <button type="button" className="cursor-default font-black text-[var(--brand)]" aria-disabled="true">Register later</button>
              </p>
            </form>

            <div className="mt-10 grid gap-4 border-t border-slate-100 pt-6 text-xs font-black text-slate-500 sm:grid-cols-3">
              {trustBadges.map((badge) => (
                <div key={badge} className="flex items-center justify-center gap-2">
                  <span className="grid h-7 w-7 place-items-center rounded-full bg-slate-100 text-slate-500">✓</span>
                  {badge}
                </div>
              ))}
            </div>
            <p className="mt-6 text-center text-xs font-semibold text-slate-400">© 2026 WorkspaceEcom. All rights reserved.</p>
          </div>
        </div>
      </div>
    </section>
  );
}
