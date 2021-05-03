<template>
  <div v-if="course == null">
    <h1>Microsoft Teams</h1>
    Retrieving data...
  </div>
  <TeamList v-if="course && !selected" :course="course" @edit="editTeam" />
  <TeamEditor :course="course" :team="selected" v-if="selected" @close="selected = null" />
</template>

<script lang="ts">
import { Options, Vue } from 'vue-class-component';
import { CourseInfo, Team } from '@/models/CourseInfo';
import TeamList from './TeamList.vue';
import TeamEditor from './TeamEditor.vue';
import axios from 'axios';

@Options({
  components: { TeamList, TeamEditor }
})
export default class Main extends Vue {
  course: CourseInfo | null = null;
  selected: Team | null = null;

  public created(): void {
    axios.defaults.headers.common['Authorization'] = `Bearer ${window.location.hash.substr(1)}`;
    axios.get(process.env.VUE_APP_ENDPOINT + '/CourseInfo').then(resp => this.course = resp.data);
  }

  editTeam(team: Team): void {
    this.selected = team;
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
