<template>
  <div v-if="course == null">
    <h1>Microsoft Teams</h1>
    Retrieving data...
  </div>
  <TeamList v-if="course && !selected" :course="course" @edit="editTeam" :canEdit="canEdit" @sync="sync" />
  <TeamEditor :course="course" :team="selected" v-if="selected" @delete="deleteSelected" @close="selected = null" @save="selected = null; sync()" />
  <LoadingScreen text="Synchronizing" v-if="isSyncing" />
</template>

<script lang="ts">
import { Options, Vue } from 'vue-class-component';
import { CourseInfo, Team } from '@/models/CourseInfo';
import TeamList from './TeamList.vue';
import TeamEditor from './TeamEditor.vue';
import LoadingScreen from './LoadingScreen.vue';
import axios from 'axios';
import jwt_decode from 'jwt-decode';

@Options({
  components: { TeamList, TeamEditor, LoadingScreen }
})
export default class Main extends Vue {
  course: CourseInfo | null = null;
  selected: Team | null = null;
  canEdit = false;
  isSyncing = false;

  public created(): void {
    const token = window.location.hash.substr(1);
    const jwt = jwt_decode<{ [claim: string]: string }>(token);
    this.canEdit = jwt["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] == "Teacher";

    axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
    axios.get(process.env.VUE_APP_ENDPOINT + '/CourseInfo').then(resp => this.course = resp.data);
  }

  deleteSelected(): void {
    this.course!.teams = this.course!.teams.filter(t => t != this.selected);
    this.selected = null;
  }

  editTeam(team: Team): void {
    this.selected = team;
  }

  sync(): void {
    this.isSyncing = true;
    axios.post(process.env.VUE_APP_ENDPOINT + '/CourseInfo/Sync').then(s => this.isSyncing = false);
  }
}
</script>

<style scoped lang="scss">
  h1 > div {
    font-size: 18px;
    margin-top: -5px;
    color: #777;
    font-weight: normal;
  }
</style>
