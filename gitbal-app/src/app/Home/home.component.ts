// home.component.ts
import { Component } from '@angular/core';
import { GitlabService } from '../Services/gitlab.service';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-home',
  imports: [FormsModule, CommonModule],
  templateUrl: './home.component.html',
  providers: [GitlabService]
})
export class HomeComponent {
  username: string = '';
  token: string = '';
  profile: any;
  projects: any;
  removeLog: any;

  constructor(private gitlabService: GitlabService) {}

  onSubmit() {
    // Handle form submission (could save the username and token in a service or local storage)
  }

  getProfile() {
    this.gitlabService.getProfile(this.token, this.username).subscribe(
      (data) => {
        this.projects = data.projects;
        //alert(`Profile: ${JSON.stringify(data)}`);
      },
      (error) => {
        alert('Error fetching profile');
      }
    );
  }

  getProjects() {
    // Call the API for projects if needed
    this.gitlabService.getProjects(this.token).subscribe(
      (data) => {
        this.profile = data;
        //alert(`Profile: ${JSON.stringify(data)}`);
      },
      (error) => {
        alert('Error fetching profile');
      }
    );
  }

  removeUser() {
    this.gitlabService.removeUser(this.token, this.username).subscribe(
      (data) => {
        this.removeLog = data;
      },
      (error) => {
        alert('Error removing user');
      }
    );
  }

}

