import { Routes } from '@angular/router';
import { Role } from './models/user';
import { ItemPage } from './pages/item/item';
import { MyWorkPage } from './pages/my-work/my-work';
import { OverviewPage } from './pages/overview/overview';
import { SignInPage } from './pages/sign-in/sign-in';
import { homeGuard, signedInGuard } from './services/guards';

const staff: readonly Role[] = ['Supervisor', 'Manager'];

export const routes: Routes = [
  { path: 'sign-in', component: SignInPage, title: 'Sign in' },
  { path: 'my-work', component: MyWorkPage, canActivate: [signedInGuard], title: 'My work' },
  {
    path: 'overview',
    component: OverviewPage,
    canActivate: [signedInGuard],
    data: { roles: staff },
    title: 'Overview',
  },
  { path: 'items/:id', component: ItemPage, canActivate: [signedInGuard], title: 'Work item' },
  { path: '', pathMatch: 'full', canActivate: [homeGuard], children: [] },
  { path: '**', redirectTo: '' },
];
