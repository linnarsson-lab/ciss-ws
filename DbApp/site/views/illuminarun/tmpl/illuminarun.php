<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
//  $illuminaruns = $this->illuminaruns;
  foreach ($this->illuminaruns as $lane) {
    $irilluminarunid = $lane->illuminarunid;
    $irdbid = $lane->id;
    $irrundate = $lane->rundate;
    $irrunno = $lane->runno;
    $ircopystatus = $lane->status;
    $irtitle = $lane->title;
    $irlabbookpage = $lane->labbookpage;
    $irrundocument = $lane->rundocument;
    $ircomment = $lane->comment;
    $iruser = $lane->user;
    $irtime = $lane->time;
  }

#######       READ in directory inof from cofig file    ######################################
    $xmlfile = JPATH_COMPONENT_ADMINISTRATOR.DS.'config.txt';
    $string = rtrim(file_get_contents($xmlfile));
    $words = preg_split("/\s/", $string);
    $xmlstring = file_get_contents($words[1]);
    preg_match("/<RunsFolder\>(.*)<\/RunsFolder>/", $xmlstring, $matches);
    $runsfolder = $matches[1];
    preg_match("/<ProjectsFolder\>(.*)<\/ProjectsFolder>/", $xmlstring, $matches);
    $projectsfolder = $matches[1];
    preg_match("/<UploadsFolder>(.*)<\/UploadsFolder>/", $xmlstring, $matches);
    $uploadsfolder = $matches[1];
    $uploadslinkfolder = "/uploads/" ;#. $uploadsfolder;
##############################################################################################


  if ($irrundocument != "") {
    $irrundocument = "<a href='" . $uploadslinkfolder . $irrundocument . "' target='_blank'>$irrundocument</a>";
  }
  $editlink = "<a href=index.php?option=com_dbapp&view=illuminarun&layout=edit&controller=illuminarun&searchid=" 
           . $irdbid . "&Itemid=" . $itemid . ">Edit this record</a>";
  $editlane = "<a href=index.php?option=com_dbapp&view=illuminarun&layout=editlanes&controller=illuminarun&searchid=" 
           . $irdbid . "&Itemid=" . $itemid . ">Edit these lanes</a>";
  $sendlink = "";
  if ($ircopystatus == 'copied') {
    $sendlink = "<a href=index.php?option=com_dbapp&view=illuminarun&layout=mailfq&controller=illuminarun&searchid="
           . $irdbid . "&Itemid=" . $itemid . ">Email fastq files</a>";
  }

    echo "<h1>Illumina run $irilluminarunid </h1>
           <div class='illuminarun'><fieldset>   
             <legend>Run data &nbsp; &nbsp; $editlink $sendlink</legend>";
    echo "<table>
            <tr><td>Run date:</td><td> $irrundate </td><td rowspan='6'>";
    echo ($irrundocument == "")? "(No run doc loaded)" : "Run document<br/> $irrundocument ";
    echo "  &nbsp;</td></tr>
            <tr><td>Title:&nbsp;</td><td> $irtitle </td></tr>
            <tr><td>RunNo:&nbsp;</td><td> $irrunno </td></tr>
            <tr><td>Status:&nbsp;</td><td> $ircopystatus </td></tr>
<!--            <tr><td>Lab book/page:</td><td> $irlabbookpage </td></tr> -->
            <tr><td>Comment:&nbsp;</td><td> $ircomment </td></tr>
            <tr><td>User:&nbsp;</td><td> $iruser </td</tr>
            <tr><td>Latest&nbsp;edit:&nbsp;</td><td> $irtime </td></tr>
          </table>
         </fieldset></div>";

    echo "<div class='lanes'><fieldset>
           <legend>Lanes in the Illumina run &nbsp; &nbsp; $editlane </legend>";
    echo "<table>
           <tr>
            <th>SampleId&nbsp;</th>
            <th>Batch</th>
            <th>P.I.&nbsp;</th>
            <th>Laneno</th>
            <th>Cycles</th>
            <th>Conc</th>
            <th>Yield</th>
            <th>Comment</th>
            <th>User</th>
            <th>Last change</th>
           </tr>";
  $boxid = "";
  foreach ($this->illuminaruns as $lane) {
    echo "<tr>
            <td><nobr>";
    if ($lane->Sid === null)
      echo " ? ";
    else if ($lane->Sid === "0")
      echo "EMPTY";
    else
      echo "<a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
            . $lane->projectid . "&Itemid=" . $itemid . ">" . $lane->plateid . "</a>&nbsp;";
    echo "  </nobr></td>
            <td>";
    if ($lane->Sid === "0" || $lane->Sid === null)
      echo " ";
    else
      echo $lane->batchtitle;
    echo    "&nbsp;</td>
            <td>" . $lane->pi . "&nbsp;</td>
            <td>" . $lane->laneno . "</td>
            <td>" . $lane->cycles . "</td>
            <td>" . $lane->molarconcentration . "</td>
            <td>" . $lane->yield . "</td>
            <td>" . $lane->Lcomment . "</td>
            <td>" . $lane->Luser . "</td>
            <td>&nbsp;" . $lane->Ltime . "</td>";
    echo "</tr>";
  }
    echo "</table></fieldset></div>";
    echo "<br />";
    echo "<a href=index.php?option=com_dbapp&view=illuminaruns&Itemid=" . $itemid . ">Return to Illumina runs list</a>";
?>
