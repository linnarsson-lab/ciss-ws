<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewProject extends JView {

	public function display($tpl = null) {
		// get the Data
		$form = $this->get('Form');
		$item = $this->get('Item');
//		$script = $this->get('Script');
		// Check for errors.
		if (count($errors = $this->get('Errors'))) {
			JError::raiseError(500, implode('<br />', $errors));
			return false;
		}
		// Assign the Data
		$this->form = $form;
		$this->item = $item;
//		$this->script = $script;
		// Set the toolbar
		$this->addToolBar();
		// Display the template
		parent::display($tpl);
		// Set the document
		$this->setDocument();
	}

	protected function addToolBar() {
		JRequest::setVar('hidemainmenu', true);
		$user = JFactory::getUser();
		$userId = $user->id;
		$isNew = $this->item->id == 0;
		$canDo = DbAppHelper::getActions($this->item->id);
		JToolBarHelper::title($isNew ? JText::_('DbApp Manager Project New') : JText::_('DbApp Manager Project'), 'Project');
		// Built the actions for new and existing records.
		if ($isNew) {
			// For new records, check the create permission.
			if ($canDo->get('core.create')) {
				JToolBarHelper::apply('project.apply', 'JTOOLBAR_APPLY');
				JToolBarHelper::save('project.save', 'JTOOLBAR_SAVE');
				JToolBarHelper::custom('project.save2new', 'save-new.png', 'save-new_f2.png', 'JTOOLBAR_SAVE_AND_NEW', false);
			}
			JToolBarHelper::cancel('project.cancel', 'JTOOLBAR_CANCEL');
		}	else {
			if ($canDo->get('core.edit'))	{
				// We can save the new record
				JToolBarHelper::apply('project.apply', 'JTOOLBAR_APPLY');
				JToolBarHelper::save('project.save', 'JTOOLBAR_SAVE');
				// We can save this record, but check the create permission to see if we can return to make a new one.
				if ($canDo->get('core.create')) {
					JToolBarHelper::custom('project.save2new', 'save-new.png', 'save-new_f2.png', 'JTOOLBAR_SAVE_AND_NEW', false);
				}
			}
			if ($canDo->get('core.create')) {
				JToolBarHelper::custom('project.save2copy', 'save-copy.png', 'save-copy_f2.png', 'JTOOLBAR_SAVE_AS_COPY', false);
			}
			JToolBarHelper::cancel('project.cancel', 'JTOOLBAR_CLOSE');
		}
	}

	protected function setDocument() {
		$isNew = $this->item->id == 0;
		$document = JFactory::getDocument();
		$document->setTitle($isNew ? JText::_('DbApp Project Create') : JText::_('DbApp Project Edit'));
//		$document->addScript(JURI::root() . $this->script);
		$document->addScript(JURI::root());
		JText::script('DbApp Project Unacceptable');
	}

}
